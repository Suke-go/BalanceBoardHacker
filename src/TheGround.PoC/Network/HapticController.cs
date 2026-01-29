using System;
using TheGround.PoC.Audio;

namespace TheGround.PoC.Network;

/// <summary>
/// Haptic controller for processing remote commands and managing vibration state.
/// </summary>
public class HapticController
{
    private readonly AudioOutputManager _audioManager;
    private DateTime _lastCommandTime;
    private DateTime _vibrationStartTime;
    private bool _isConnected;
    
    // Timeouts
    private const double HeartbeatTimeoutSec = 3.0;
    private const double MaxVibrationDurationSec = 30.0;  // Safety limit
    
    /// <summary>Whether vibration is currently active.</summary>
    public bool IsVibrating => _audioManager.IsPlaying;
    
    /// <summary>Current signal type.</summary>
    public SignalType CurrentType => _audioManager.Generator.SignalType;
    
    /// <summary>Current velocity (for SnowTexture).</summary>
    public float CurrentVelocity => _audioManager.Generator.Velocity;
    
    /// <summary>Whether Quest client is connected (received command recently).</summary>
    public bool IsClientConnected => _isConnected;
    
    /// <summary>Event fired when connection state changes.</summary>
    public event Action<bool>? OnConnectionChanged;
    
    /// <summary>Event fired when command is processed.</summary>
    public event Action<string>? OnCommandProcessed;
    
    public HapticController(AudioOutputManager audioManager)
    {
        _audioManager = audioManager ?? throw new ArgumentNullException(nameof(audioManager));
        _lastCommandTime = DateTime.MinValue;
    }
    
    /// <summary>
    /// Process a command string from Quest.
    /// </summary>
    public void ProcessCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;
        
        _lastCommandTime = DateTime.UtcNow;
        if (!_isConnected)
        {
            _isConnected = true;
            OnConnectionChanged?.Invoke(true);
        }
        
        var parts = command.Trim().ToUpperInvariant().Split(',');
        var cmd = parts[0];
        
        try
        {
            switch (cmd)
            {
                case "VIB_START":
                    HandleVibStart(parts);
                    break;
                    
                case "VIB_STOP":
                    StopVibration();
                    break;
                    
                case "VIB_VELOCITY":
                    if (parts.Length >= 2 && float.TryParse(parts[1], out float v))
                        SetVelocity(v);
                    break;
                    
                case "VIB_PULSE":
                    HandleVibPulse(parts);
                    break;
                    
                case "CAL_START":
                    // Forward to MainForm via event
                    OnCommandProcessed?.Invoke("CAL_START");
                    break;
                    
                case "RESET":
                    StopVibration();
                    _audioManager.Generator.Reset();
                    break;
                    
                case "PING":
                    // Heartbeat, no action needed
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HapticController] Error: {ex.Message}");
        }
        
        OnCommandProcessed?.Invoke(command);
    }
    
    private void HandleVibStart(string[] parts)
    {
        // VIB_START,<type>,<amplitude>
        SignalType type = SignalType.SnowTexture;
        float amplitude = 0.5f;
        
        if (parts.Length >= 2)
        {
            type = parts[1].ToUpperInvariant() switch
            {
                "SINE" => SignalType.Sine,
                "NOISE" => SignalType.BandLimitedNoise,
                "SNOW" => SignalType.SnowTexture,
                _ => SignalType.SnowTexture
            };
        }
        
        if (parts.Length >= 3 && float.TryParse(parts[2], out float amp))
        {
            amplitude = Math.Clamp(amp, 0f, 1f);
        }
        
        StartVibration(type, amplitude);
    }
    
    private void HandleVibPulse(string[] parts)
    {
        // VIB_PULSE,<duration>,<amplitude>
        float duration = 0.2f;
        float amplitude = 1.0f;
        
        if (parts.Length >= 2 && float.TryParse(parts[1], out float d))
            duration = Math.Clamp(d, 0.05f, 1f);
        
        if (parts.Length >= 3 && float.TryParse(parts[2], out float a))
            amplitude = Math.Clamp(a, 0f, 1f);
        
        Pulse(duration, amplitude);
    }
    
    /// <summary>
    /// Start vibration with specified type and amplitude.
    /// </summary>
    public void StartVibration(SignalType type, float amplitude)
    {
        _audioManager.Generator.SignalType = type;
        _audioManager.Generator.Amplitude = Math.Clamp(amplitude, 0f, 1f);
        _audioManager.Play();
        _vibrationStartTime = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Stop vibration.
    /// </summary>
    public void StopVibration()
    {
        _audioManager.Stop();
    }
    
    /// <summary>
    /// Set velocity for SnowTexture signal.
    /// </summary>
    public void SetVelocity(float velocity)
    {
        _audioManager.Generator.Velocity = Math.Clamp(velocity, 0f, 1f);
    }
    
    /// <summary>
    /// Short vibration pulse.
    /// </summary>
    public async void Pulse(float durationSec, float amplitude)
    {
        var prevType = _audioManager.Generator.SignalType;
        var prevAmp = _audioManager.Generator.Amplitude;
        var wasPlaying = _audioManager.IsPlaying;
        
        _audioManager.Generator.SignalType = SignalType.Sine;
        _audioManager.Generator.Amplitude = amplitude;
        _audioManager.Generator.Frequency = 30f;
        _audioManager.Play();
        
        await System.Threading.Tasks.Task.Delay((int)(durationSec * 1000));
        
        if (wasPlaying)
        {
            _audioManager.Generator.SignalType = prevType;
            _audioManager.Generator.Amplitude = prevAmp;
        }
        else
        {
            _audioManager.Stop();
        }
    }
    
    /// <summary>
    /// Check heartbeat and safety limits. Call from timer (~50ms).
    /// </summary>
    public void CheckSafety()
    {
        var now = DateTime.UtcNow;
        
        // Heartbeat timeout - client disconnected
        if (_isConnected && (now - _lastCommandTime).TotalSeconds > HeartbeatTimeoutSec)
        {
            _isConnected = false;
            StopVibration();
            OnConnectionChanged?.Invoke(false);
        }
        
        // Max vibration duration safety
        if (IsVibrating && (now - _vibrationStartTime).TotalSeconds > MaxVibrationDurationSec)
        {
            StopVibration();
            System.Diagnostics.Debug.WriteLine("[HapticController] Safety timeout - vibration stopped");
        }
    }
}
