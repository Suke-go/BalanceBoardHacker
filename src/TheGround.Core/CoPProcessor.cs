using System;

namespace TheGround.Core
{
    /// <summary>
    /// CoP processing result.
    /// </summary>
    public struct CoPResult
    {
        /// <summary>Compensated CoP X position in mm.</summary>
        public float X;
        
        /// <summary>Compensated CoP Y position in mm.</summary>
        public float Y;
        
        /// <summary>Net weight in kg (after tare).</summary>
        public float Weight;
        
        /// <summary>Raw CoP X before compensation.</summary>
        public float RawX;
        
        /// <summary>Raw CoP Y before compensation.</summary>
        public float RawY;
        
        /// <summary>Whether the reading is valid (sufficient weight on board).</summary>
        public bool IsValid;
        
        /// <summary>Timestamp in milliseconds.</summary>
        public long TimestampMs;
    }

    /// <summary>
    /// Main CoP processor with calibration and adaptive vibration compensation.
    /// Thread-safe for single producer/consumer pattern.
    /// </summary>
    public class CoPProcessor
    {
        // Board dimensions (Wii Balance Board)
        private const float BoardLengthMm = 433f;
        private const float BoardWidthMm = 238f;
        private const float MinWeight = 5f;  // kg
        
        // Calibration state
        private float _tareWeight = 0f;
        private float _offsetX = 0f, _offsetY = 0f;
        private bool _isCalibrated = false;
        private bool _isCalibrating = false;
        private int _calibrationCount = 0;
        private float _calSumX = 0f, _calSumY = 0f, _calSumW = 0f;
        private const int CalibrationSamples = 180;  // 3 seconds at 60Hz
        
        // Adaptive compensation (NLMS multi-harmonic)
        private const int NumHarmonics = 3;
        private const int NumWeights = NumHarmonics * 2;
        private readonly float[] _wxComp = new float[NumWeights];
        private readonly float[] _wyComp = new float[NumWeights];
        private float _mu = 0.1f;
        private const float Epsilon = 1e-6f;
        private float _internalPhase = 0f;
        
        // State
        private readonly float _sampleRate;
        private float _vibrationFrequency = 30f;
        private bool _compensationEnabled = false;
        private bool _isConverged = false;
        private float _snr = 0f;
        private float _mse = 0f;
        private int _sampleCount = 0;
        
        // Running metrics
        private float _powerSum = 0f;
        private float _errorSum = 0f;
        
        /// <summary>Whether calibration has been performed.</summary>
        public bool IsCalibrated => _isCalibrated;
        
        /// <summary>Whether calibration is in progress.</summary>
        public bool IsCalibrating => _isCalibrating;
        
        /// <summary>Calibration progress (0-100).</summary>
        public int CalibrationProgress => _isCalibrating ? (_calibrationCount * 100 / CalibrationSamples) : 0;
        
        /// <summary>Enable/disable vibration compensation.</summary>
        public bool CompensationEnabled
        {
            get => _compensationEnabled;
            set => _compensationEnabled = value;
        }
        
        /// <summary>Vibration frequency for compensation (Hz).</summary>
        public float VibrationFrequency
        {
            get => _vibrationFrequency;
            set => _vibrationFrequency = Math.Max(1f, value);
        }
        
        /// <summary>Whether compensation has converged.</summary>
        public bool IsConverged => _isConverged;
        
        /// <summary>SNR improvement in dB.</summary>
        public float SnrImprovement => _snr;
        
        /// <summary>NLMS step size (0.001 - 1.0).</summary>
        public float StepSize
        {
            get => _mu;
            set => _mu = Math.Max(0.001f, Math.Min(1f, value));
        }
        
        /// <summary>
        /// Create a new CoP processor.
        /// </summary>
        /// <param name="sampleRate">Expected sample rate in Hz.</param>
        public CoPProcessor(float sampleRate = 60f)
        {
            _sampleRate = sampleRate;
        }
        
        /// <summary>
        /// Process sensor values and return CoP result.
        /// </summary>
        /// <param name="topLeft">Top-left sensor (kg)</param>
        /// <param name="topRight">Top-right sensor (kg)</param>
        /// <param name="bottomLeft">Bottom-left sensor (kg)</param>
        /// <param name="bottomRight">Bottom-right sensor (kg)</param>
        /// <param name="vibrationActive">Whether vibration is currently active</param>
        /// <param name="audioPhase">Audio output phase (0-2π), -1 for internal tracking</param>
        public CoPResult Process(float topLeft, float topRight, float bottomLeft, float bottomRight,
                                 bool vibrationActive = false, float audioPhase = -1f)
        {
            float totalWeight = topLeft + topRight + bottomLeft + bottomRight;
            bool isValid = totalWeight >= MinWeight;
            
            // Calculate raw CoP
            float rawX = 0f, rawY = 0f;
            if (isValid)
            {
                float invW = 1f / totalWeight;
                rawX = (BoardWidthMm * 0.5f) * ((topRight + bottomRight) - (topLeft + bottomLeft)) * invW;
                rawY = (BoardLengthMm * 0.5f) * ((topLeft + topRight) - (bottomLeft + bottomRight)) * invW;
            }
            
            // Calibration accumulation
            if (_isCalibrating && isValid)
            {
                _calSumX += rawX;
                _calSumY += rawY;
                _calSumW += totalWeight;
                _calibrationCount++;
                
                if (_calibrationCount >= CalibrationSamples)
                {
                    _offsetX = _calSumX / _calibrationCount;
                    _offsetY = _calSumY / _calibrationCount;
                    _tareWeight = _calSumW / _calibrationCount;
                    _isCalibrated = true;
                    _isCalibrating = false;
                }
            }
            
            // Apply calibration offset
            float copX = rawX - _offsetX;
            float copY = rawY - _offsetY;
            float netWeight = totalWeight - _tareWeight;
            
            // Vibration compensation (NLMS)
            if (_compensationEnabled && vibrationActive && isValid)
            {
                float phase = audioPhase >= 0 ? audioPhase : _internalPhase;
                if (audioPhase < 0)
                {
                    _internalPhase += 2f * (float)Math.PI * _vibrationFrequency / _sampleRate;
                    if (_internalPhase > 2f * (float)Math.PI)
                        _internalPhase -= 2f * (float)Math.PI;
                }
                
                // Multi-harmonic reference
                Span<float> refs = stackalloc float[NumWeights];
                for (int h = 1; h <= NumHarmonics; h++)
                {
                    float hp = phase * h;
                    refs[(h - 1) * 2] = (float)Math.Sin(hp);
                    refs[(h - 1) * 2 + 1] = (float)Math.Cos(hp);
                }
                
                // Estimate interference
                float estX = 0f, estY = 0f;
                for (int i = 0; i < NumWeights; i++)
                {
                    estX += _wxComp[i] * refs[i];
                    estY += _wyComp[i] * refs[i];
                }
                
                // Subtract
                copX -= estX;
                copY -= estY;
                
                // NLMS update
                float refPower = 0f;
                for (int i = 0; i < NumWeights; i++)
                    refPower += refs[i] * refs[i];
                float norm = _mu / (refPower + Epsilon);
                
                for (int i = 0; i < NumWeights; i++)
                {
                    _wxComp[i] += norm * copX * refs[i];
                    _wyComp[i] += norm * copY * refs[i];
                    _wxComp[i] = Math.Max(-100f, Math.Min(100f, _wxComp[i]));
                    _wyComp[i] = Math.Max(-100f, Math.Min(100f, _wyComp[i]));
                }
                
                // Update metrics
                _powerSum = 0.99f * _powerSum + 0.01f * (rawX * rawX + rawY * rawY);
                _errorSum = 0.95f * _errorSum + 0.05f * (copX * copX + copY * copY);
                _sampleCount++;
                
                if (_sampleCount > 60)
                {
                    _snr = 10f * (float)Math.Log10(_powerSum / (_errorSum + Epsilon));
                    _isConverged = _snr > 6f;
                }
            }
            
            return new CoPResult
            {
                X = copX,
                Y = copY,
                Weight = netWeight,
                RawX = rawX - _offsetX,
                RawY = rawY - _offsetY,
                IsValid = isValid,
                TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }
        
        /// <summary>
        /// Start calibration (3 seconds of averaging).
        /// </summary>
        public void StartCalibration()
        {
            _isCalibrating = true;
            _calibrationCount = 0;
            _calSumX = _calSumY = _calSumW = 0f;
        }
        
        /// <summary>
        /// Cancel ongoing calibration.
        /// </summary>
        public void CancelCalibration()
        {
            _isCalibrating = false;
        }
        
        /// <summary>
        /// Reset all state including calibration and compensation.
        /// </summary>
        public void Reset()
        {
            _isCalibrated = false;
            _isCalibrating = false;
            _calibrationCount = 0;
            _calSumX = _calSumY = _calSumW = 0f;
            _offsetX = _offsetY = _tareWeight = 0f;
            
            Array.Clear(_wxComp, 0, NumWeights);
            Array.Clear(_wyComp, 0, NumWeights);
            _internalPhase = 0f;
            _sampleCount = 0;
            _snr = 0f;
            _mse = 0f;
            _powerSum = _errorSum = 0f;
            _isConverged = false;
        }
        
        /// <summary>
        /// Reset only compensation weights (keep calibration).
        /// </summary>
        public void ResetCompensation()
        {
            Array.Clear(_wxComp, 0, NumWeights);
            Array.Clear(_wyComp, 0, NumWeights);
            _internalPhase = 0f;
            _sampleCount = 0;
            _snr = 0f;
            _powerSum = _errorSum = 0f;
            _isConverged = false;
        }
        
        /// <summary>
        /// Get harmonic amplitude for given axis (0=X, 1=Y) and harmonic (1-3).
        /// </summary>
        public float GetHarmonicAmplitude(int axis, int harmonic)
        {
            if (harmonic < 1 || harmonic > NumHarmonics) return 0f;
            var w = axis == 0 ? _wxComp : _wyComp;
            int i = (harmonic - 1) * 2;
            return (float)Math.Sqrt(w[i] * w[i] + w[i + 1] * w[i + 1]);
        }
    }
}
