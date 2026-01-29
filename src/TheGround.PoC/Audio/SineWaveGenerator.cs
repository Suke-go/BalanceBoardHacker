using System;
using NAudio.Wave;

namespace TheGround.PoC.Audio;

/// <summary>
/// Signal type for haptic output.
/// </summary>
public enum SignalType
{
    Sine,
    BandLimitedNoise,  // White noise filtered around center frequency
    SnowTexture        // Multi-layer noise simulating ski-on-snow vibration
}

/// <summary>
/// Generates stereo signals for haptic feedback via Bass Shaker.
/// Supports sine wave, band-limited noise, and snow texture.
/// </summary>
public class SineWaveGenerator : ISampleProvider
{
    private readonly WaveFormat _waveFormat;
    private readonly Random _random = new();
    private double _phase;
    
    // Noise filter state (simple 2-pole bandpass)
    private float _noiseState1, _noiseState2;
    private float _filterCoeffA, _filterCoeffB;
    
    // Snow texture: multi-band noise layers
    private float _snowLowState1, _snowLowState2;   // 16-24Hz - Ski bending
    private float _snowMidState1, _snowMidState2;   // 25-40Hz - Snow grain
    private float _snowHighState1, _snowHighState2; // 80-120Hz - Ice crystals
    private float _snowLowA, _snowLowB;
    private float _snowMidA, _snowMidB;
    private float _snowHighA, _snowHighB;

    /// <summary>
    /// Creates a new stereo signal generator.
    /// </summary>
    public SineWaveGenerator(int sampleRate = 48000)
    {
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);
        UpdateNoiseFilter();
        UpdateSnowFilter();
    }

    /// <inheritdoc />
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>
    /// Signal type (Sine, BandLimitedNoise, or SnowTexture).
    /// </summary>
    public SignalType SignalType { get; set; } = SignalType.Sine;

    /// <summary>
    /// Frequency of the sine wave or center frequency for noise (Hz).
    /// </summary>
    public float Frequency
    {
        get => _frequency;
        set
        {
            _frequency = value;
            UpdateNoiseFilter();
        }
    }
    private float _frequency = 30f;
    
    /// <summary>
    /// Bandwidth for noise in Hz (default: 20Hz around center).
    /// </summary>
    public float NoiseBandwidth
    {
        get => _noiseBandwidth;
        set
        {
            _noiseBandwidth = value;
            UpdateNoiseFilter();
        }
    }
    private float _noiseBandwidth = 20f;

    /// <summary>
    /// Amplitude of the signal (0.0 to 1.0).
    /// </summary>
    public float Amplitude { get; set; } = 0.5f;
    
    /// <summary>
    /// Velocity for snow texture (0.0 to 1.0, affects texture intensity).
    /// Higher velocity = more high-frequency content (ice crystal feel).
    /// </summary>
    public float Velocity { get; set; } = 0.5f;

    /// <summary>
    /// Whether output is enabled.
    /// </summary>
    public bool IsPlaying { get; set; }

    /// <summary>
    /// Enable output on channel 1 (left).
    /// </summary>
    public bool EnableChannel1 { get; set; } = true;

    /// <summary>
    /// Enable output on channel 2 (right).
    /// </summary>
    public bool EnableChannel2 { get; set; } = true;
    
    /// <summary>
    /// Current output phase (0-2π), for synchronization with compensation.
    /// </summary>
    public double CurrentPhase => _phase;

    private void UpdateNoiseFilter()
    {
        // Simple resonant bandpass filter coefficients
        float omega = 2f * MathF.PI * _frequency / _waveFormat.SampleRate;
        float bw = 2f * MathF.PI * _noiseBandwidth / _waveFormat.SampleRate;
        _filterCoeffA = MathF.Exp(-bw);
        _filterCoeffB = 2f * MathF.Cos(omega);
    }
    
    private void UpdateSnowFilter()
    {
        // Snow texture: 3 frequency bands based on ski vibration research
        // Low: 16-24Hz (ski bending mode)
        SetBandpass(20f, 8f, out _snowLowA, out _snowLowB);
        // Mid: 25-40Hz (snow grain interaction)
        SetBandpass(32f, 15f, out _snowMidA, out _snowMidB);
        // High: 80-120Hz (ice crystal/hard snow)
        SetBandpass(100f, 40f, out _snowHighA, out _snowHighB);
    }
    
    private void SetBandpass(float centerHz, float bandwidthHz, out float a, out float b)
    {
        float omega = 2f * MathF.PI * centerHz / _waveFormat.SampleRate;
        float bw = 2f * MathF.PI * bandwidthHz / _waveFormat.SampleRate;
        a = MathF.Exp(-bw);
        b = 2f * MathF.Cos(omega);
    }

    /// <inheritdoc />
    public int Read(float[] buffer, int offset, int count)
    {
        if (!IsPlaying)
        {
            Array.Clear(buffer, offset, count);
            return count;
        }

        double phaseIncrement = 2 * Math.PI * Frequency / _waveFormat.SampleRate;

        // Stereo: buffer[i] = left (ch1), buffer[i+1] = right (ch2)
        for (int i = 0; i < count; i += 2)
        {
            float sample;
            
            if (SignalType == SignalType.Sine)
            {
                sample = Amplitude * (float)Math.Sin(_phase);
            }
            else if (SignalType == SignalType.BandLimitedNoise)
            {
                // Band-limited white noise using resonant filter
                float whiteNoise = (float)(_random.NextDouble() * 2.0 - 1.0);
                float filtered = whiteNoise + _filterCoeffA * _filterCoeffB * _noiseState1 
                               - _filterCoeffA * _filterCoeffA * _noiseState2;
                _noiseState2 = _noiseState1;
                _noiseState1 = filtered;
                sample = Amplitude * filtered * 0.3f;
            }
            else // SnowTexture
            {
                sample = GenerateSnowSample();
            }

            buffer[offset + i] = EnableChannel1 ? sample : 0f;      // Left (ch1)
            buffer[offset + i + 1] = EnableChannel2 ? sample : 0f;  // Right (ch2)

            _phase += phaseIncrement;
            if (_phase > 2 * Math.PI)
                _phase -= 2 * Math.PI;
        }

        return count;
    }
    
    private float GenerateSnowSample()
    {
        // Generate 3 independent white noise sources
        float noise1 = (float)(_random.NextDouble() * 2.0 - 1.0);
        float noise2 = (float)(_random.NextDouble() * 2.0 - 1.0);
        float noise3 = (float)(_random.NextDouble() * 2.0 - 1.0);
        
        // Low band (16-24Hz) - Ski structure bending, always present
        float low = noise1 + _snowLowA * _snowLowB * _snowLowState1 
                  - _snowLowA * _snowLowA * _snowLowState2;
        _snowLowState2 = _snowLowState1;
        _snowLowState1 = low;
        
        // Mid band (25-40Hz) - Snow grain interaction, velocity dependent
        float mid = noise2 + _snowMidA * _snowMidB * _snowMidState1 
                  - _snowMidA * _snowMidA * _snowMidState2;
        _snowMidState2 = _snowMidState1;
        _snowMidState1 = mid;
        
        // High band (80-120Hz) - Ice crystals, high velocity only
        float high = noise3 + _snowHighA * _snowHighB * _snowHighState1 
                   - _snowHighA * _snowHighA * _snowHighState2;
        _snowHighState2 = _snowHighState1;
        _snowHighState1 = high;
        
        // Mix based on velocity
        // Low speed: mostly low freq (soft snow feel)
        // High speed: add mid + high (hard/icy feel)
        float v = Math.Clamp(Velocity, 0f, 1f);
        float lowGain = 0.5f + 0.3f * v;           // 0.5 -> 0.8
        float midGain = 0.2f + 0.5f * v;           // 0.2 -> 0.7
        float highGain = v * v * 0.4f;             // 0 -> 0.4 (quadratic)
        
        float mixed = low * lowGain + mid * midGain + high * highGain;
        
        // Normalize and apply amplitude
        return Amplitude * mixed * 0.25f * (0.5f + v);
    }

    /// <summary>
    /// Reset the phase and filter state.
    /// </summary>
    public void Reset()
    {
        _phase = 0;
        _noiseState1 = 0;
        _noiseState2 = 0;
        _snowLowState1 = _snowLowState2 = 0;
        _snowMidState1 = _snowMidState2 = 0;
        _snowHighState1 = _snowHighState2 = 0;
    }
}

