using System;
using System.Numerics;
using TheGround.PoC.Audio;

namespace TheGround.PoC.Demo;

/// <summary>
/// Demo mode type for Balance Board vibration feedback.
/// </summary>
public enum DemoMode
{
    /// <summary>No demo active - manual/network control.</summary>
    Off,
    
    /// <summary>
    /// Ski Jump mode: Forward lean = acceleration (high vibration),
    /// backward lean = braking (low vibration). Simulates ski resistance.
    /// </summary>
    SkiJump,
    
    /// <summary>
    /// Left-Right Tilt mode: Leaning left increases left channel,
    /// leaning right increases right channel. Useful for tilt feedback.
    /// </summary>
    LeftRightTilt,
    
    /// <summary>
    /// Unified Ski mode: Combines realistic ski physics.
    /// - Backward lean = strong vibration (edging resistance)
    /// - Forward lean = weak vibration (smooth glide)
    /// - Left/Right turn = OPPOSITE side vibrates more (outside ski pressure)
    /// </summary>
    Unified
}

/// <summary>
/// Controls demo vibration modes that respond to Balance Board CoP input.
/// Designed for standalone demonstration without Unity connection.
/// </summary>
public class DemoModeController
{
    private readonly SineWaveGenerator _generator;
    private readonly AudioOutputManager _audioManager;
    
    // Board dimensions (mm) - from CoPCalculator
    private const float BoardWidthMm = 238f;   // X axis, ±119mm
    private const float BoardLengthMm = 433f;  // Y axis, ±216.5mm
    
    // Dead zone to prevent jitter at center (mm) - smaller = more responsive
    private const float DeadZoneMm = 8f;
    
    // === Ski Jump Mode Parameters ===
    // DRAMATIC version: large amplitude range for clear tactile difference
    // - Forward lean (Y > 0): acceleration, strong vibration + high-freq texture
    // - Backward lean (Y < 0): braking/floating, almost no vibration
    // - Neutral: light vibration
    private const float SkiForwardThresholdMm = 25f;   // Easier to reach max (was 30)
    private const float SkiBackwardThresholdMm = -20f; // Easier to reach min (was -25)
    private const float SkiBaseAmplitude = 0.25f;      // Neutral (was 0.3)
    private const float SkiMaxAmplitude = 1.0f;        // FULL POWER forward (was 0.8)
    private const float SkiMinAmplitude = 0.03f;       // Almost silent backward (was 0.1)
    private const float SkiBaseVelocity = 0.35f;       // Base velocity (was 0.4)
    private const float SkiMaxVelocity = 1.0f;         // Max velocity (high-freq content)
    private const float SkiMinVelocity = 0.1f;         // Very low velocity when braking
    
    // Frequency modulation for ski mode (adds another dimension of change)
    private const float SkiBaseFrequency = 28f;        // Neutral frequency
    private const float SkiMaxFrequency = 45f;         // High freq when accelerating
    private const float SkiMinFrequency = 18f;         // Low rumble when braking
    
    // === Left-Right Tilt Mode Parameters ===
    // Intuitive tilt feedback: heavier side vibrates more
    private const float TiltSensitivityMm = 45f;       // More sensitive (was 60)
    private const float TiltBaseAmplitude = 0.5f;      // Center amplitude (was 0.4)
    private const float TiltMaxAmplitude = 1.0f;       // Maximum on tilted side (was 0.85)
    private const float TiltMinAmplitude = 0.05f;      // Almost off on opposite (was 0.1)
    
    // Smoothing - higher = more responsive
    private float _smoothedAmplitude = 0f;
    private float _smoothedVelocity = 0f;
    private float _smoothedFrequency = SkiBaseFrequency;
    private float _smoothedLeftGain = 0.5f;
    private float _smoothedRightGain = 0.5f;
    private const float SmoothingFactor = 0.25f;  // More responsive (was 0.15)
    
    private DemoMode _currentMode = DemoMode.Off;
    
    /// <summary>
    /// Current demo mode.
    /// </summary>
    public DemoMode CurrentMode
    {
        get => _currentMode;
        set
        {
            if (_currentMode != value)
            {
                _currentMode = value;
                OnModeChanged();
            }
        }
    }
    
    /// <summary>
    /// Whether a demo mode is currently active.
    /// </summary>
    public bool IsActive => _currentMode != DemoMode.Off;
    
    /// <summary>
    /// Event fired when demo mode changes.
    /// </summary>
    public event Action<DemoMode>? OnDemoModeChanged;
    
    /// <summary>
    /// Event fired each update with debug info.
    /// </summary>
    public event Action<string>? OnDebugInfo;
    
    public DemoModeController(AudioOutputManager audioManager)
    {
        _audioManager = audioManager ?? throw new ArgumentNullException(nameof(audioManager));
        _generator = audioManager.Generator;
    }
    
    private void OnModeChanged()
    {
        if (_currentMode == DemoMode.Off)
        {
            // Stop vibration when switching off
            _audioManager.Stop();
            _generator.EnableChannel1 = true;
            _generator.EnableChannel2 = true;
        }
        else
        {
            // Initialize for demo mode
            _generator.SignalType = SignalType.SnowTexture;
            _generator.EnableChannel1 = true;
            _generator.EnableChannel2 = true;
            _generator.Frequency = 30f;
            
            // Reset smoothing
            _smoothedAmplitude = SkiBaseAmplitude;
            _smoothedVelocity = SkiBaseVelocity;
            _smoothedLeftGain = 0.5f;
            _smoothedRightGain = 0.5f;
            
            // Start audio if not playing
            if (!_audioManager.IsPlaying)
            {
                _audioManager.Initialize(null, latencyMs: 50);
                _audioManager.Play();
            }
        }
        
        OnDemoModeChanged?.Invoke(_currentMode);
    }
    
    /// <summary>
    /// Update vibration based on current CoP position.
    /// Call this at sensor update rate (~60Hz).
    /// </summary>
    /// <param name="copMm">Center of pressure in mm (X=left/right, Y=front/back)</param>
    /// <param name="isOnBoard">Whether user is on the board</param>
    public void Update(Vector2 copMm, bool isOnBoard)
    {
        if (_currentMode == DemoMode.Off)
            return;
        
        if (!isOnBoard)
        {
            // User stepped off - reduce to minimum
            _smoothedAmplitude = Lerp(_smoothedAmplitude, 0.05f, SmoothingFactor);
            _generator.Amplitude = _smoothedAmplitude;
            return;
        }
        
        switch (_currentMode)
        {
            case DemoMode.SkiJump:
                UpdateSkiJumpMode(copMm);
                break;
            case DemoMode.LeftRightTilt:
                UpdateLeftRightTiltMode(copMm);
                break;
            case DemoMode.Unified:
                UpdateUnifiedMode(copMm);
                break;
        }
    }
    
    /// <summary>
    /// Ski Jump Mode: Forward/backward weight shift controls vibration intensity.
    /// Forward lean = more vibration (ski on snow friction) + higher frequency
    /// Backward lean = less vibration (floating/braking) + lower frequency
    /// </summary>
    private void UpdateSkiJumpMode(Vector2 copMm)
    {
        float y = copMm.Y;
        
        // Apply dead zone
        if (Math.Abs(y) < DeadZoneMm)
            y = 0f;
        
        // Calculate target amplitude, velocity, and frequency based on Y position
        float targetAmplitude;
        float targetVelocity;
        float targetFrequency;
        
        if (y > DeadZoneMm)
        {
            // Forward lean: STRONG vibration + high frequency (aggressive acceleration feel)
            float forwardRatio = Math.Clamp((y - DeadZoneMm) / (SkiForwardThresholdMm - DeadZoneMm), 0f, 1f);
            // Use quadratic curve for more dramatic ramp-up
            float curvedRatio = forwardRatio * forwardRatio;
            targetAmplitude = Lerp(SkiBaseAmplitude, SkiMaxAmplitude, curvedRatio);
            targetVelocity = Lerp(SkiBaseVelocity, SkiMaxVelocity, forwardRatio);
            targetFrequency = Lerp(SkiBaseFrequency, SkiMaxFrequency, forwardRatio);
        }
        else if (y < -DeadZoneMm)
        {
            // Backward lean: WEAK vibration + low frequency (floating/braking feel)
            float backwardRatio = Math.Clamp((-y - DeadZoneMm) / (-SkiBackwardThresholdMm - DeadZoneMm), 0f, 1f);
            // Use quadratic curve for dramatic fade-out
            float curvedRatio = backwardRatio * backwardRatio;
            targetAmplitude = Lerp(SkiBaseAmplitude, SkiMinAmplitude, curvedRatio);
            targetVelocity = Lerp(SkiBaseVelocity, SkiMinVelocity, backwardRatio);
            targetFrequency = Lerp(SkiBaseFrequency, SkiMinFrequency, backwardRatio);
        }
        else
        {
            // Neutral
            targetAmplitude = SkiBaseAmplitude;
            targetVelocity = SkiBaseVelocity;
            targetFrequency = SkiBaseFrequency;
        }
        
        // Smooth transitions
        _smoothedAmplitude = Lerp(_smoothedAmplitude, targetAmplitude, SmoothingFactor);
        _smoothedVelocity = Lerp(_smoothedVelocity, targetVelocity, SmoothingFactor);
        _smoothedFrequency = Lerp(_smoothedFrequency, targetFrequency, SmoothingFactor);
        
        // Apply to generator
        _generator.Amplitude = _smoothedAmplitude;
        _generator.Velocity = _smoothedVelocity;
        _generator.Frequency = _smoothedFrequency;
        
        // Both channels equal in ski jump mode
        _generator.EnableChannel1 = true;
        _generator.EnableChannel2 = true;
        _generator.Channel1Amplitude = 1.0f;
        _generator.Channel2Amplitude = 1.0f;
        
        OnDebugInfo?.Invoke($"Ski: Y={y:F1} Amp={_smoothedAmplitude:F2} Freq={_smoothedFrequency:F0}Hz");
    }
    
    /// <summary>
    /// Left-Right Tilt Mode: Weight shift left/right controls per-channel amplitude.
    /// Tilt left = left channel louder, right quieter
    /// Tilt right = right channel louder, left quieter
    /// </summary>
    private void UpdateLeftRightTiltMode(Vector2 copMm)
    {
        float x = copMm.X;
        
        // Apply dead zone
        if (Math.Abs(x) < DeadZoneMm)
            x = 0f;
        
        // Calculate left/right amplitude ratio
        // X positive = right lean
        float tiltRatio = Math.Clamp(x / TiltSensitivityMm, -1f, 1f);
        
        float targetLeftGain, targetRightGain;
        
        if (tiltRatio > 0)
        {
            // Leaning right: right channel stronger
            targetRightGain = Lerp(TiltBaseAmplitude, TiltMaxAmplitude, tiltRatio);
            targetLeftGain = Lerp(TiltBaseAmplitude, TiltMinAmplitude, tiltRatio);
        }
        else if (tiltRatio < 0)
        {
            // Leaning left: left channel stronger
            targetLeftGain = Lerp(TiltBaseAmplitude, TiltMaxAmplitude, -tiltRatio);
            targetRightGain = Lerp(TiltBaseAmplitude, TiltMinAmplitude, -tiltRatio);
        }
        else
        {
            // Center
            targetLeftGain = TiltBaseAmplitude;
            targetRightGain = TiltBaseAmplitude;
        }
        
        // Smooth transitions
        _smoothedLeftGain = Lerp(_smoothedLeftGain, targetLeftGain, SmoothingFactor);
        _smoothedRightGain = Lerp(_smoothedRightGain, targetRightGain, SmoothingFactor);
        
        // Use per-channel amplitude for precise stereo control
        // Normalize to 0-1 range relative to max amplitude
        float maxGain = Math.Max(_smoothedLeftGain, _smoothedRightGain);
        _generator.Amplitude = maxGain;
        
        // Set per-channel multipliers (relative to main amplitude)
        if (maxGain > 0.01f)
        {
            _generator.Channel1Amplitude = _smoothedLeftGain / maxGain;
            _generator.Channel2Amplitude = _smoothedRightGain / maxGain;
        }
        else
        {
            _generator.Channel1Amplitude = 1.0f;
            _generator.Channel2Amplitude = 1.0f;
        }
        
        // Keep both channels enabled
        _generator.EnableChannel1 = true;
        _generator.EnableChannel2 = true;
        
        OnDebugInfo?.Invoke($"Tilt: X={x:F1}mm L={_smoothedLeftGain:F2} R={_smoothedRightGain:F2}");
    }
    
    /// <summary>
    /// Unified Ski Mode: Realistic ski physics combining front/back and left/right.
    /// - Backward lean = strong vibration (edging/braking resistance)
    /// - Forward lean = weak vibration (smooth aerodynamic glide)
    /// - Left/Right turn = OPPOSITE side vibrates more (outside ski carries more weight)
    /// </summary>
    private void UpdateUnifiedMode(Vector2 copMm)
    {
        float x = copMm.X;
        float y = copMm.Y;
        
        // Apply dead zones
        if (Math.Abs(x) < DeadZoneMm) x = 0f;
        if (Math.Abs(y) < DeadZoneMm) y = 0f;
        
        // === FRONT/BACK: Controls base amplitude ===
        // INVERTED from SkiJump: backward = strong, forward = weak
        // This simulates: back = edging resistance, forward = smooth glide
        float baseAmplitude;
        float targetVelocity;
        float targetFrequency;
        
        if (y < -DeadZoneMm)
        {
            // BACKWARD lean: STRONG vibration (edging, snow resistance)
            float backRatio = Math.Clamp((-y - DeadZoneMm) / (-SkiBackwardThresholdMm - DeadZoneMm), 0f, 1f);
            float curved = backRatio * backRatio;
            baseAmplitude = Lerp(SkiBaseAmplitude, SkiMaxAmplitude, curved);
            targetVelocity = Lerp(SkiBaseVelocity, SkiMaxVelocity, backRatio);
            targetFrequency = Lerp(SkiBaseFrequency, SkiMaxFrequency, backRatio);
        }
        else if (y > DeadZoneMm)
        {
            // FORWARD lean: WEAK vibration (smooth tuck position)
            float forwardRatio = Math.Clamp((y - DeadZoneMm) / (SkiForwardThresholdMm - DeadZoneMm), 0f, 1f);
            float curved = forwardRatio * forwardRatio;
            baseAmplitude = Lerp(SkiBaseAmplitude, SkiMinAmplitude, curved);
            targetVelocity = Lerp(SkiBaseVelocity, SkiMinVelocity, forwardRatio);
            targetFrequency = Lerp(SkiBaseFrequency, SkiMinFrequency, forwardRatio);
        }
        else
        {
            // Neutral
            baseAmplitude = SkiBaseAmplitude;
            targetVelocity = SkiBaseVelocity;
            targetFrequency = SkiBaseFrequency;
        }
        
        // === LEFT/RIGHT: Controls channel balance (INVERSE - outside ski stronger) ===
        // Leaning LEFT (x < 0) = turning left = RIGHT (outside) ski gets more pressure
        // Leaning RIGHT (x > 0) = turning right = LEFT (outside) ski gets more pressure
        float tiltRatio = Math.Clamp(x / TiltSensitivityMm, -1f, 1f);
        
        float leftMultiplier = 1.0f;
        float rightMultiplier = 1.0f;
        
        if (tiltRatio > 0.1f)
        {
            // Leaning RIGHT = turning right = LEFT (outside) stronger
            leftMultiplier = Lerp(1.0f, 1.5f, tiltRatio);    // Boost outside
            rightMultiplier = Lerp(1.0f, 0.3f, tiltRatio);   // Reduce inside
        }
        else if (tiltRatio < -0.1f)
        {
            // Leaning LEFT = turning left = RIGHT (outside) stronger
            rightMultiplier = Lerp(1.0f, 1.5f, -tiltRatio);  // Boost outside
            leftMultiplier = Lerp(1.0f, 0.3f, -tiltRatio);   // Reduce inside
        }
        
        // Calculate final channel amplitudes
        float targetLeftAmp = baseAmplitude * leftMultiplier;
        float targetRightAmp = baseAmplitude * rightMultiplier;
        
        // Clamp to valid range
        targetLeftAmp = Math.Clamp(targetLeftAmp, 0.02f, 1.0f);
        targetRightAmp = Math.Clamp(targetRightAmp, 0.02f, 1.0f);
        
        // Smooth all values
        _smoothedLeftGain = Lerp(_smoothedLeftGain, targetLeftAmp, SmoothingFactor);
        _smoothedRightGain = Lerp(_smoothedRightGain, targetRightAmp, SmoothingFactor);
        _smoothedVelocity = Lerp(_smoothedVelocity, targetVelocity, SmoothingFactor);
        _smoothedFrequency = Lerp(_smoothedFrequency, targetFrequency, SmoothingFactor);
        
        // Apply to generator
        float maxAmp = Math.Max(_smoothedLeftGain, _smoothedRightGain);
        _generator.Amplitude = maxAmp;
        _generator.Velocity = _smoothedVelocity;
        _generator.Frequency = _smoothedFrequency;
        
        // Per-channel multipliers
        if (maxAmp > 0.01f)
        {
            _generator.Channel1Amplitude = _smoothedLeftGain / maxAmp;
            _generator.Channel2Amplitude = _smoothedRightGain / maxAmp;
        }
        else
        {
            _generator.Channel1Amplitude = 1.0f;
            _generator.Channel2Amplitude = 1.0f;
        }
        
        _generator.EnableChannel1 = true;
        _generator.EnableChannel2 = true;
        
        // Debug info shows both axes
        string dir = y > DeadZoneMm ? "F" : (y < -DeadZoneMm ? "B" : "-");
        string turn = x > DeadZoneMm ? "→R" : (x < -DeadZoneMm ? "L←" : "--");
        OnDebugInfo?.Invoke($"Uni: {dir}{turn} L={_smoothedLeftGain:F2} R={_smoothedRightGain:F2} {_smoothedFrequency:F0}Hz");
    }
    
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
