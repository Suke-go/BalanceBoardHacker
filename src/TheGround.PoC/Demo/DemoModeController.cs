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
    Unified,
    
    /// <summary>
    /// Four Direction mode: Distinct feedback for each quadrant.
    /// Front-Left, Front-Right, Back-Left, Back-Right.
    /// </summary>
    FourDirection
}

/// <summary>
/// Controls demo vibration modes that respond to Balance Board CoP input.
/// Designed for standalone demonstration without Unity connection.
/// Supports calibration for personalized center position.
/// </summary>
public class DemoModeController
{
    private readonly SineWaveGenerator _generator;
    private readonly AudioOutputManager _audioManager;
    
    // Board dimensions (mm) - from CoPCalculator
    private const float BoardWidthMm = 238f;   // X axis, ±119mm
    private const float BoardLengthMm = 433f;  // Y axis, ±216.5mm
    
    // === Calibration ===
    private Vector2 _calibratedCenter = Vector2.Zero;  // Calibrated center position
    private bool _isCalibrated = false;
    
    // Dead zone for responsiveness (calibration overrides this)
    private const float DeadZoneMm = 5f;
    
    // === Ski Jump Mode Parameters ===
    // EXTREME version: maximum contrast for obvious tactile difference
    private const float SkiForwardThresholdMm = 18f;   // Very easy to reach max
    private const float SkiBackwardThresholdMm = -15f; // Very easy to reach min
    private const float SkiBaseAmplitude = 0.20f;      // Light neutral
    private const float SkiMaxAmplitude = 1.0f;        // FULL POWER
    private const float SkiMinAmplitude = 0.01f;       // Essentially OFF
    private const float SkiBaseVelocity = 0.30f;       // Base velocity
    private const float SkiMaxVelocity = 1.0f;         // Max texture
    private const float SkiMinVelocity = 0.05f;        // Almost no texture
    
    // Frequency modulation - EXTREME range for obvious tactile change
    // Forward (smooth glide) = very low, Backward (resistance) = very high
    private const float SkiBaseFrequency = 40f;         // Neutral frequency
    private const float SkiMaxFrequency = 120f;         // High freq for resistance/edging (backward)
    private const float SkiMinFrequency = 10f;          // Low rumble for smooth glide (forward)
    
    // === Left-Right Tilt Mode Parameters ===
    // Very sensitive for dramatic stereo effect
    private const float TiltSensitivityMm = 30f;       // Even more sensitive
    private const float TiltBaseAmplitude = 0.5f;      // Center amplitude
    private const float TiltMaxAmplitude = 1.0f;       // Full on tilted side
    private const float TiltMinAmplitude = 0.02f;      // Essentially off on opposite
    
    // === Two-Stage Smoothing ===
    // Fast stage: quick response to changes
    // Slow stage: smooth out the fast stage output
    private float _fastAmplitude = 0f;
    private float _slowAmplitude = 0f;
    private float _fastVelocity = 0f;
    private float _slowVelocity = 0f;
    private float _fastFrequency = SkiBaseFrequency;
    private float _slowFrequency = SkiBaseFrequency;
    private float _fastLeftGain = 0.5f;
    private float _slowLeftGain = 0.5f;
    private float _fastRightGain = 0.5f;
    private float _slowRightGain = 0.5f;
    
    private const float FastSmoothingFactor = 0.6f;   // Quick response to input
    private const float SlowSmoothingFactor = 0.15f;  // Smooth final output
    
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
    /// Whether the center position has been calibrated.
    /// </summary>
    public bool IsCalibrated => _isCalibrated;
    
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
    
    /// <summary>
    /// Calibrate the center position. Call this when user is standing neutral.
    /// </summary>
    public void CalibrateCenter(Vector2 currentCopMm)
    {
        _calibratedCenter = currentCopMm;
        _isCalibrated = true;
        OnDebugInfo?.Invoke($"Calibrated: center=({currentCopMm.X:F1}, {currentCopMm.Y:F1})mm");
    }
    
    /// <summary>
    /// Reset calibration to default (0,0).
    /// </summary>
    public void ResetCalibration()
    {
        _calibratedCenter = Vector2.Zero;
        _isCalibrated = false;
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
            
            // Reset two-stage smoothing
            _fastAmplitude = _slowAmplitude = SkiBaseAmplitude;
            _fastVelocity = _slowVelocity = SkiBaseVelocity;
            _fastFrequency = _slowFrequency = SkiBaseFrequency;
            _fastLeftGain = _slowLeftGain = 0.5f;
            _fastRightGain = _slowRightGain = 0.5f;
            
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
        
        // Apply calibration offset
        Vector2 adjustedCoP = copMm - _calibratedCenter;
        
        if (!isOnBoard)
        {
            // User stepped off - reduce to minimum smoothly
            _fastAmplitude = Lerp(_fastAmplitude, 0.05f, FastSmoothingFactor);
            _slowAmplitude = Lerp(_slowAmplitude, _fastAmplitude, SlowSmoothingFactor);
            _generator.Amplitude = _slowAmplitude;
            return;
        }
        
        switch (_currentMode)
        {
            case DemoMode.SkiJump:
                UpdateSkiJumpMode(adjustedCoP);
                break;
            case DemoMode.LeftRightTilt:
                UpdateLeftRightTiltMode(adjustedCoP);
                break;
            case DemoMode.Unified:
                UpdateUnifiedMode(adjustedCoP);
                break;
            case DemoMode.FourDirection:
                UpdateFourDirectionMode(adjustedCoP);
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
        
        // Two-stage smoothing: fast response + smooth output
        _fastAmplitude = Lerp(_fastAmplitude, targetAmplitude, FastSmoothingFactor);
        _slowAmplitude = Lerp(_slowAmplitude, _fastAmplitude, SlowSmoothingFactor);
        _fastVelocity = Lerp(_fastVelocity, targetVelocity, FastSmoothingFactor);
        _slowVelocity = Lerp(_slowVelocity, _fastVelocity, SlowSmoothingFactor);
        _fastFrequency = Lerp(_fastFrequency, targetFrequency, FastSmoothingFactor);
        _slowFrequency = Lerp(_slowFrequency, _fastFrequency, SlowSmoothingFactor);
        
        // Apply smoothed values to generator
        _generator.Amplitude = _slowAmplitude;
        _generator.Velocity = _slowVelocity;
        _generator.Frequency = _slowFrequency;
        
        // Both channels equal in ski jump mode
        _generator.EnableChannel1 = true;
        _generator.EnableChannel2 = true;
        _generator.Channel1Amplitude = 1.0f;
        _generator.Channel2Amplitude = 1.0f;
        
        OnDebugInfo?.Invoke($"Ski: Y={y:F1} Amp={_slowAmplitude:F2} Freq={_slowFrequency:F0}Hz");
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
        
        // Two-stage smoothing: fast response + smooth output
        _fastLeftGain = Lerp(_fastLeftGain, targetLeftGain, FastSmoothingFactor);
        _slowLeftGain = Lerp(_slowLeftGain, _fastLeftGain, SlowSmoothingFactor);
        _fastRightGain = Lerp(_fastRightGain, targetRightGain, FastSmoothingFactor);
        _slowRightGain = Lerp(_slowRightGain, _fastRightGain, SlowSmoothingFactor);
        
        // Use per-channel amplitude for precise stereo control
        float maxGain = Math.Max(_slowLeftGain, _slowRightGain);
        _generator.Amplitude = maxGain;
        
        // Set per-channel multipliers (relative to main amplitude)
        if (maxGain > 0.01f)
        {
            _generator.Channel1Amplitude = _slowLeftGain / maxGain;
            _generator.Channel2Amplitude = _slowRightGain / maxGain;
        }
        else
        {
            _generator.Channel1Amplitude = 1.0f;
            _generator.Channel2Amplitude = 1.0f;
        }
        
        // Keep both channels enabled
        _generator.EnableChannel1 = true;
        _generator.EnableChannel2 = true;
        
        OnDebugInfo?.Invoke($"Tilt: X={x:F1}mm L={_slowLeftGain:F2} R={_slowRightGain:F2}");
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
        
        // COMPLETELY OFF inside ski - maximum contrast
        if (tiltRatio > 0.02f)  // Very low threshold
        {
            // Leaning RIGHT = turning right = LEFT (outside) full, RIGHT off
            leftMultiplier = Lerp(1.0f, 1.5f, tiltRatio);     // Boost outside
            rightMultiplier = Lerp(1.0f, 0.0f, tiltRatio);    // COMPLETELY OFF inside
        }
        else if (tiltRatio < -0.02f)
        {
            // Leaning LEFT = turning left = RIGHT (outside) full, LEFT off
            rightMultiplier = Lerp(1.0f, 1.5f, -tiltRatio);   // Boost outside
            leftMultiplier = Lerp(1.0f, 0.0f, -tiltRatio);    // COMPLETELY OFF inside
        }
        
        // Calculate final channel amplitudes
        float targetLeftAmp = baseAmplitude * leftMultiplier;
        float targetRightAmp = baseAmplitude * rightMultiplier;
        
        // Clamp - allow 0 for complete off
        targetLeftAmp = Math.Clamp(targetLeftAmp, 0.0f, 1.0f);
        targetRightAmp = Math.Clamp(targetRightAmp, 0.0f, 1.0f);
        
        // Two-stage smoothing: fast response + smooth output
        _fastLeftGain = Lerp(_fastLeftGain, targetLeftAmp, FastSmoothingFactor);
        _slowLeftGain = Lerp(_slowLeftGain, _fastLeftGain, SlowSmoothingFactor);
        _fastRightGain = Lerp(_fastRightGain, targetRightAmp, FastSmoothingFactor);
        _slowRightGain = Lerp(_slowRightGain, _fastRightGain, SlowSmoothingFactor);
        _fastVelocity = Lerp(_fastVelocity, targetVelocity, FastSmoothingFactor);
        _slowVelocity = Lerp(_slowVelocity, _fastVelocity, SlowSmoothingFactor);
        _fastFrequency = Lerp(_fastFrequency, targetFrequency, FastSmoothingFactor);
        _slowFrequency = Lerp(_slowFrequency, _fastFrequency, SlowSmoothingFactor);
        
        // Apply to generator
        float maxAmp = Math.Max(_slowLeftGain, _slowRightGain);
        _generator.Amplitude = maxAmp;
        _generator.Velocity = _slowVelocity;
        _generator.Frequency = _slowFrequency;
        
        // Per-channel multipliers
        if (maxAmp > 0.01f)
        {
            _generator.Channel1Amplitude = _slowLeftGain / maxAmp;
            _generator.Channel2Amplitude = _slowRightGain / maxAmp;
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
        OnDebugInfo?.Invoke($"Uni: {dir}{turn} L={_slowLeftGain:F2} R={_slowRightGain:F2} {_slowFrequency:F0}Hz");
    }
    
    /// <summary>
    /// Four Direction Mode: Distinct feedback for each quadrant.
    /// - Front-Left: Low freq, left channel
    /// - Front-Right: Low freq, right channel  
    /// - Back-Left: High freq, left channel
    /// - Back-Right: High freq, right channel
    /// </summary>
    private void UpdateFourDirectionMode(Vector2 copMm)
    {
        float x = copMm.X;
        float y = copMm.Y;
        
        // Determine quadrant and intensity
        float xRatio = Math.Clamp(x / TiltSensitivityMm, -1f, 1f);
        float yRatio = Math.Clamp(y / SkiForwardThresholdMm, -1f, 1f);
        
        // Amplitude based on distance from center
        float distance = MathF.Sqrt(xRatio * xRatio + yRatio * yRatio);
        float targetAmp = Lerp(0.1f, 1.0f, Math.Min(distance, 1f));
        
        // Frequency based on front/back (front=low, back=high)
        float targetFreq = Lerp(SkiBaseFrequency, yRatio < 0 ? SkiMaxFrequency : SkiMinFrequency, Math.Abs(yRatio));
        
        // Channel balance based on left/right
        float leftMult = xRatio < 0 ? Lerp(1f, 1.5f, -xRatio) : Lerp(1f, 0f, xRatio);
        float rightMult = xRatio > 0 ? Lerp(1f, 1.5f, xRatio) : Lerp(1f, 0f, -xRatio);
        
        float targetLeft = targetAmp * leftMult;
        float targetRight = targetAmp * rightMult;
        
        targetLeft = Math.Clamp(targetLeft, 0f, 1f);
        targetRight = Math.Clamp(targetRight, 0f, 1f);
        
        // Two-stage smoothing
        _fastLeftGain = Lerp(_fastLeftGain, targetLeft, FastSmoothingFactor);
        _slowLeftGain = Lerp(_slowLeftGain, _fastLeftGain, SlowSmoothingFactor);
        _fastRightGain = Lerp(_fastRightGain, targetRight, FastSmoothingFactor);
        _slowRightGain = Lerp(_slowRightGain, _fastRightGain, SlowSmoothingFactor);
        _fastFrequency = Lerp(_fastFrequency, targetFreq, FastSmoothingFactor);
        _slowFrequency = Lerp(_slowFrequency, _fastFrequency, SlowSmoothingFactor);
        
        // Apply
        float maxAmp = Math.Max(_slowLeftGain, _slowRightGain);
        _generator.Amplitude = Math.Max(maxAmp, 0.05f);
        _generator.Frequency = _slowFrequency;
        
        if (maxAmp > 0.01f)
        {
            _generator.Channel1Amplitude = _slowLeftGain / maxAmp;
            _generator.Channel2Amplitude = _slowRightGain / maxAmp;
        }
        else
        {
            _generator.Channel1Amplitude = 1f;
            _generator.Channel2Amplitude = 1f;
        }
        
        _generator.EnableChannel1 = true;
        _generator.EnableChannel2 = true;
        
        // Debug quadrant
        string quad = (y >= 0 ? "F" : "B") + (x >= 0 ? "R" : "L");
        OnDebugInfo?.Invoke($"4Dir: {quad} L={_slowLeftGain:F2} R={_slowRightGain:F2} {_slowFrequency:F0}Hz");
    }
    
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
