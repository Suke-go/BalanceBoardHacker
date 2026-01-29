using System;
using System.Numerics;

namespace TheGround.PoC.SignalProcessing;

/// <summary>
/// Research-grade vibration compensation for CoP measurements.
/// Implements NLMS (Normalized LMS) with multi-harmonic cancellation.
/// 
/// Algorithm: Adaptive Multi-Harmonic Interference Cancellation (AMHIC)
/// 
/// Key features for CHI/UIST/VRST level:
/// 1. NLMS with automatic step-size normalization (faster convergence, stable)
/// 2. Multi-harmonic compensation (f, 2f, 3f) for non-sinusoidal vibration
/// 3. Phase-coherent reference from audio output
/// 4. Real-time quality metrics (MSE, SNR improvement)
/// 5. Automatic convergence detection
/// 
/// Reference: Widrow & Stearns (1985) "Adaptive Signal Processing"
/// </summary>
public class AdaptiveVibrationCompensator
{
    // Number of harmonics to compensate (1 = fundamental only, 3 = f + 2f + 3f)
    private const int NumHarmonics = 3;
    private const int NumWeightsPerAxis = NumHarmonics * 2;  // sin + cos per harmonic
    
    // NLMS parameters
    private readonly float[] _weightsX;  // [sin(f), cos(f), sin(2f), cos(2f), sin(3f), cos(3f)]
    private readonly float[] _weightsY;
    private float _mu = 0.1f;            // NLMS step size (0 < μ < 2)
    private const float Epsilon = 1e-6f;  // Regularization to avoid division by zero
    private const float MaxWeight = 100f;
    
    // Quality metrics
    private float _powerX = 0f, _powerY = 0f;  // Running estimate of input power
    private float _mseX = 0f, _mseY = 0f;       // Mean squared error
    private float _snrImprovement = 0f;
    private const float PowerDecay = 0.99f;
    private const float MseDecay = 0.95f;
    
    // Convergence detection
    private int _sampleCount = 0;
    private bool _isConverged = false;
    private float _convergenceThreshold = 0.01f;  // MSE threshold for convergence
    private int _convergenceWindowSize = 60;       // ~1 second at 60Hz
    private readonly float[] _mseHistory;
    private int _mseHistoryIndex = 0;
    
    // State
    private float _vibrationFrequency = 30f;
    private float _sampleRate = 60f;
    private float _internalPhase = 0f;
    private bool _isEnabled = false;
    private bool _isLearning = true;
    
    // Fallback: Butterworth low-pass (when compensation disabled)
    private readonly NotchFilter _notchX, _notchY;
    private bool _useNotchFallback = false;
    
    // Properties
    public bool IsEnabled { get => _isEnabled; set => _isEnabled = value; }
    public bool IsLearning { get => _isLearning; set => _isLearning = value; }
    public bool UseNotchFallback { get => _useNotchFallback; set => _useNotchFallback = value; }
    public float VibrationFrequency 
    { 
        get => _vibrationFrequency; 
        set 
        {
            _vibrationFrequency = value;
            _notchX.SetFrequency(value);
            _notchY.SetFrequency(value);
        }
    }
    public float StepSize { get => _mu; set => _mu = Math.Clamp(value, 0.001f, 1.9f); }
    
    // Quality metrics (for UI display)
    public bool IsConverged => _isConverged;
    public float MseX => _mseX;
    public float MseY => _mseY;
    public float SnrImprovement => _snrImprovement;
    public float[] WeightsX => _weightsX;
    public float[] WeightsY => _weightsY;
    
    // Computed amplitude per harmonic
    public float GetHarmonicAmplitude(int axis, int harmonic)
    {
        if (harmonic < 1 || harmonic > NumHarmonics) return 0f;
        var w = axis == 0 ? _weightsX : _weightsY;
        int i = (harmonic - 1) * 2;
        return MathF.Sqrt(w[i] * w[i] + w[i + 1] * w[i + 1]);
    }
    
    public event Action<string>? OnStatusChanged;
    
    public AdaptiveVibrationCompensator(float sampleRate = 60f)
    {
        _sampleRate = sampleRate;
        _weightsX = new float[NumWeightsPerAxis];
        _weightsY = new float[NumWeightsPerAxis];
        _mseHistory = new float[_convergenceWindowSize];
        
        _notchX = new NotchFilter(30f, sampleRate, 0.95f);
        _notchY = new NotchFilter(30f, sampleRate, 0.95f);
    }
    
    /// <summary>
    /// Process CoP and remove vibration interference using NLMS multi-harmonic cancellation.
    /// </summary>
    /// <param name="rawCoP">Raw CoP measurement</param>
    /// <param name="vibrationActive">Whether vibration output is active</param>
    /// <param name="audioPhase">Phase from audio generator (0-2π), or -1 to use internal</param>
    public Vector2 Process(Vector2 rawCoP, bool vibrationActive, double audioPhase = -1)
    {
        if (!_isEnabled)
            return rawCoP;
        
        if (!vibrationActive)
        {
            _internalPhase = 0f;
            return rawCoP;
        }
        
        // Notch fallback mode
        if (_useNotchFallback)
        {
            return new Vector2(
                _notchX.Process(rawCoP.X),
                _notchY.Process(rawCoP.Y)
            );
        }
        
        // Determine phase
        float phase = audioPhase >= 0 ? (float)audioPhase : _internalPhase;
        if (audioPhase < 0)
        {
            _internalPhase += 2f * MathF.PI * _vibrationFrequency / _sampleRate;
            if (_internalPhase > 2f * MathF.PI)
                _internalPhase -= 2f * MathF.PI;
        }
        
        // Generate multi-harmonic reference vector
        Span<float> refSignals = stackalloc float[NumWeightsPerAxis];
        for (int h = 1; h <= NumHarmonics; h++)
        {
            float harmPhase = phase * h;
            refSignals[(h - 1) * 2] = MathF.Sin(harmPhase);
            refSignals[(h - 1) * 2 + 1] = MathF.Cos(harmPhase);
        }
        
        // Compute estimated interference
        float estX = 0f, estY = 0f;
        for (int i = 0; i < NumWeightsPerAxis; i++)
        {
            estX += _weightsX[i] * refSignals[i];
            estY += _weightsY[i] * refSignals[i];
        }
        
        // Subtract estimated interference
        float correctedX = rawCoP.X - estX;
        float correctedY = rawCoP.Y - estY;
        
        // NLMS weight update
        if (_isLearning)
        {
            // Compute reference power for normalization
            float refPower = 0f;
            for (int i = 0; i < NumWeightsPerAxis; i++)
                refPower += refSignals[i] * refSignals[i];
            
            float norm = _mu / (refPower + Epsilon);
            
            // Update weights: w += μ * error * reference / (||reference||² + ε)
            for (int i = 0; i < NumWeightsPerAxis; i++)
            {
                _weightsX[i] = Clamp(_weightsX[i] + norm * correctedX * refSignals[i]);
                _weightsY[i] = Clamp(_weightsY[i] + norm * correctedY * refSignals[i]);
            }
        }
        
        // Update quality metrics
        UpdateMetrics(rawCoP.X, rawCoP.Y, correctedX, correctedY);
        _sampleCount++;
        
        return new Vector2(correctedX, correctedY);
    }
    
    private float Clamp(float value) => 
        value < -MaxWeight ? -MaxWeight : (value > MaxWeight ? MaxWeight : value);
    
    private void UpdateMetrics(float rawX, float rawY, float corrX, float corrY)
    {
        // Running power estimate (for SNR calculation)
        _powerX = PowerDecay * _powerX + (1 - PowerDecay) * rawX * rawX;
        _powerY = PowerDecay * _powerY + (1 - PowerDecay) * rawY * rawY;
        
        // MSE of corrected signal at vibration frequency (should approach zero)
        float errorX = corrX * corrX;
        float errorY = corrY * corrY;
        _mseX = MseDecay * _mseX + (1 - MseDecay) * errorX;
        _mseY = MseDecay * _mseY + (1 - MseDecay) * errorY;
        
        // SNR improvement estimate (dB)
        float avgPower = (_powerX + _powerY) * 0.5f;
        float avgMse = (_mseX + _mseY) * 0.5f;
        if (avgMse > Epsilon)
            _snrImprovement = 10f * MathF.Log10(avgPower / avgMse + Epsilon);
        
        // Convergence detection
        float totalMse = _mseX + _mseY;
        _mseHistory[_mseHistoryIndex] = totalMse;
        _mseHistoryIndex = (_mseHistoryIndex + 1) % _convergenceWindowSize;
        
        if (_sampleCount >= _convergenceWindowSize)
        {
            // Check if MSE variance is low (converged)
            float sum = 0f, sumSq = 0f;
            for (int i = 0; i < _convergenceWindowSize; i++)
            {
                sum += _mseHistory[i];
                sumSq += _mseHistory[i] * _mseHistory[i];
            }
            float mean = sum / _convergenceWindowSize;
            float variance = sumSq / _convergenceWindowSize - mean * mean;
            
            bool wasConverged = _isConverged;
            _isConverged = variance < _convergenceThreshold && mean < 1.0f;
            
            if (_isConverged && !wasConverged)
                OnStatusChanged?.Invoke($"Converged! SNR improvement: {_snrImprovement:F1} dB");
        }
    }
    
    /// <summary>
    /// Reset all weights and metrics.
    /// </summary>
    public void Reset()
    {
        Array.Clear(_weightsX, 0, NumWeightsPerAxis);
        Array.Clear(_weightsY, 0, NumWeightsPerAxis);
        Array.Clear(_mseHistory, 0, _convergenceWindowSize);
        _powerX = _powerY = 0f;
        _mseX = _mseY = 0f;
        _snrImprovement = 0f;
        _sampleCount = 0;
        _isConverged = false;
        _internalPhase = 0f;
        _mseHistoryIndex = 0;
        _notchX.Reset();
        _notchY.Reset();
        
        OnStatusChanged?.Invoke("Compensator reset");
    }
    
    /// <summary>
    /// Export weights for reproducibility (save to file/log).
    /// </summary>
    public string ExportWeights()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Adaptive Vibration Compensation Weights");
        sb.AppendLine($"# Frequency: {_vibrationFrequency} Hz, Harmonics: {NumHarmonics}");
        sb.AppendLine($"# SNR Improvement: {_snrImprovement:F2} dB, Converged: {_isConverged}");
        sb.AppendLine();
        sb.AppendLine("Axis,Harmonic,Sin,Cos,Amplitude");
        for (int h = 1; h <= NumHarmonics; h++)
        {
            int i = (h - 1) * 2;
            sb.AppendLine($"X,{h},{_weightsX[i]:F4},{_weightsX[i+1]:F4},{GetHarmonicAmplitude(0, h):F4}");
            sb.AppendLine($"Y,{h},{_weightsY[i]:F4},{_weightsY[i+1]:F4},{GetHarmonicAmplitude(1, h):F4}");
        }
        return sb.ToString();
    }
}
