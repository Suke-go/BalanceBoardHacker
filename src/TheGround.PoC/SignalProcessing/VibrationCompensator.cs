using System;
using System.Numerics;

namespace TheGround.PoC.SignalProcessing;

/// <summary>
/// Compensates for known vibration frequency interference in CoP measurements.
/// Uses adaptive cancellation synchronized with the audio output phase.
/// 
/// Algorithm: Adaptive Interference Cancellation (AIC)
/// 1. Reference signal from audio output phase provides sin(ωt) and cos(ωt)
/// 2. LMS-style adaptation learns optimal cancellation weights
/// 3. Estimated vibration component is subtracted from raw CoP
/// </summary>
public class VibrationCompensator
{
    // Adaptive filter weights (for X and Y, sin and cos components)
    private float _wxSin = 0f, _wxCos = 0f;
    private float _wySin = 0f, _wyCos = 0f;
    
    // LMS adaptation parameters
    private float _learningRate = 0.001f;  // Step size (μ)
    private const float MaxWeight = 50f;    // Limit weight growth
    
    // Running state
    private float _vibrationFrequency = 30f;
    private float _sampleRate = 60f;
    private bool _isEnabled = false;
    private bool _isLearning = true;  // Continuously adapt weights
    
    // Phase tracking (when not using external reference)
    private float _internalPhase = 0f;
    
    // Alternative: Notch filter for non-adaptive mode
    private readonly NotchFilter _notchFilterX;
    private readonly NotchFilter _notchFilterY;
    private bool _useNotchFilter = false;
    
    // Statistics for monitoring
    private float _estimatedVibrationX = 0f;
    private float _estimatedVibrationY = 0f;
    
    public bool IsEnabled { get => _isEnabled; set => _isEnabled = value; }
    public bool IsLearning { get => _isLearning; set => _isLearning = value; }
    public bool UseNotchFilter { get => _useNotchFilter; set => _useNotchFilter = value; }
    public float VibrationFrequency 
    { 
        get => _vibrationFrequency; 
        set 
        {
            _vibrationFrequency = value;
            _notchFilterX.SetFrequency(value);
            _notchFilterY.SetFrequency(value);
        }
    }
    public float LearningRate { get => _learningRate; set => _learningRate = Math.Clamp(value, 0.0001f, 0.1f); }
    
    // Monitoring
    public float WeightXSin => _wxSin;
    public float WeightXCos => _wxCos;
    public float WeightYSin => _wySin;
    public float WeightYCos => _wyCos;
    public float EstimatedVibrationX => _estimatedVibrationX;
    public float EstimatedVibrationY => _estimatedVibrationY;
    
    public event Action<string>? OnStatusChanged;
    
    public VibrationCompensator(float sampleRate = 60f)
    {
        _sampleRate = sampleRate;
        _notchFilterX = new NotchFilter(30f, sampleRate, 0.95f);
        _notchFilterY = new NotchFilter(30f, sampleRate, 0.95f);
    }
    
    /// <summary>
    /// Process CoP and remove vibration interference using adaptive cancellation.
    /// </summary>
    /// <param name="rawCoP">Raw CoP measurement</param>
    /// <param name="vibrationActive">Whether vibration output is active</param>
    /// <param name="audioPhase">Phase from audio generator (0-2π), or -1 to use internal tracking</param>
    public Vector2 Process(Vector2 rawCoP, bool vibrationActive, double audioPhase = -1)
    {
        if (!_isEnabled)
        {
            return rawCoP;
        }
        
        if (!vibrationActive)
        {
            // Reset internal phase when vibration stops
            _internalPhase = 0f;
            return rawCoP;
        }
        
        // Use notch filter if selected
        if (_useNotchFilter)
        {
            return new Vector2(
                _notchFilterX.Process(rawCoP.X),
                _notchFilterY.Process(rawCoP.Y)
            );
        }
        
        // Determine phase to use
        float phase;
        if (audioPhase >= 0)
        {
            phase = (float)audioPhase;
        }
        else
        {
            // Use internal phase tracking
            phase = _internalPhase;
            _internalPhase += 2f * MathF.PI * _vibrationFrequency / _sampleRate;
            if (_internalPhase > 2f * MathF.PI)
                _internalPhase -= 2f * MathF.PI;
        }
        
        // Generate reference signals
        float sinRef = MathF.Sin(phase);
        float cosRef = MathF.Cos(phase);
        
        // Estimate vibration component using current weights
        _estimatedVibrationX = _wxSin * sinRef + _wxCos * cosRef;
        _estimatedVibrationY = _wySin * sinRef + _wyCos * cosRef;
        
        // Subtract estimated vibration to get corrected CoP
        float correctedX = rawCoP.X - _estimatedVibrationX;
        float correctedY = rawCoP.Y - _estimatedVibrationY;
        
        // LMS weight update (if learning enabled)
        if (_isLearning)
        {
            // Error = corrected signal (we want to minimize residual vibration)
            // But we can't directly measure error without knowing the true CoP
            // So we use the corrected signal's high-frequency component as error proxy
            // For now, use simplified correlation-based update
            
            float errorX = correctedX;  // Residual after cancellation
            float errorY = correctedY;
            
            // Update weights using LMS rule: w += μ * error * reference
            _wxSin = Clamp(_wxSin + _learningRate * errorX * sinRef, -MaxWeight, MaxWeight);
            _wxCos = Clamp(_wxCos + _learningRate * errorX * cosRef, -MaxWeight, MaxWeight);
            _wySin = Clamp(_wySin + _learningRate * errorY * sinRef, -MaxWeight, MaxWeight);
            _wyCos = Clamp(_wyCos + _learningRate * errorY * cosRef, -MaxWeight, MaxWeight);
        }
        
        return new Vector2(correctedX, correctedY);
    }
    
    private static float Clamp(float value, float min, float max)
    {
        return value < min ? min : (value > max ? max : value);
    }
    
    /// <summary>
    /// Reset all weights and filters.
    /// </summary>
    public void Reset()
    {
        _wxSin = _wxCos = 0f;
        _wySin = _wyCos = 0f;
        _internalPhase = 0f;
        _estimatedVibrationX = 0f;
        _estimatedVibrationY = 0f;
        _notchFilterX.Reset();
        _notchFilterY.Reset();
        OnStatusChanged?.Invoke("Vibration compensator reset");
    }
    
    /// <summary>
    /// Freeze current weights (stop learning).
    /// </summary>
    public void FreezeWeights()
    {
        _isLearning = false;
        OnStatusChanged?.Invoke($"Weights frozen: X=({_wxSin:F2},{_wxCos:F2}) Y=({_wySin:F2},{_wyCos:F2})");
    }
}

/// <summary>
/// IIR Notch filter for removing specific frequency component.
/// </summary>
public class NotchFilter
{
    private float _frequency;
    private float _sampleRate;
    private float _r;  // Pole radius (closer to 1 = narrower notch)
    
    // Coefficients
    private float _a1, _a2;
    private float _cosOmega;
    
    // State
    private float _x1, _x2;  // Input history
    private float _y1, _y2;  // Output history
    
    public NotchFilter(float frequency, float sampleRate, float poleRadius = 0.95f)
    {
        _frequency = frequency;
        _sampleRate = sampleRate;
        _r = poleRadius;
        UpdateCoefficients();
    }
    
    public void SetFrequency(float frequency)
    {
        _frequency = frequency;
        UpdateCoefficients();
        Reset();
    }
    
    private void UpdateCoefficients()
    {
        // Notch at normalized frequency
        float omega = 2f * MathF.PI * _frequency / _sampleRate;
        _cosOmega = MathF.Cos(omega);
        
        // Zero at e^(j*omega), pole at r*e^(j*omega)
        _a1 = -2f * _r * _cosOmega;
        _a2 = _r * _r;
    }
    
    public float Process(float input)
    {
        // Numerator zeros at +/- omega: (1 - 2*cos(omega)*z^-1 + z^-2)
        float output = input - 2f * _cosOmega * _x1 + _x2 
                     - _a1 * _y1 - _a2 * _y2;
        
        // Normalize gain at DC
        float dcGain = (1f - 2f * _cosOmega + 1f) / (1f + _a1 + _a2);
        if (MathF.Abs(dcGain) > 0.001f)
            output /= dcGain;
        
        // Update state
        _x2 = _x1;
        _x1 = input;
        _y2 = _y1;
        _y1 = output;
        
        return output;
    }
    
    public void Reset()
    {
        _x1 = _x2 = _y1 = _y2 = 0f;
    }
}

