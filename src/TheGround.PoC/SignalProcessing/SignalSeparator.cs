using System;

namespace TheGround.PoC.SignalProcessing;

/// <summary>
/// 2nd-order Butterworth low-pass filter implementation.
/// Used for separating CoP signal from Bass Shaker vibration.
/// </summary>
public class ButterworthFilter
{
    private readonly double _a0, _a1, _a2;
    private readonly double _b0, _b1, _b2;

    // State variables for IIR filter
    private double _x1, _x2;  // Input history
    private double _y1, _y2;  // Output history

    /// <summary>
    /// Cutoff frequency in Hz.
    /// </summary>
    public float CutoffHz { get; }

    /// <summary>
    /// Sample rate in Hz.
    /// </summary>
    public float SampleRateHz { get; }

    /// <summary>
    /// Creates a 2nd-order Butterworth low-pass filter.
    /// </summary>
    /// <param name="cutoffHz">Cutoff frequency in Hz</param>
    /// <param name="sampleRateHz">Sample rate in Hz</param>
    public ButterworthFilter(float cutoffHz, float sampleRateHz)
    {
        CutoffHz = cutoffHz;
        SampleRateHz = sampleRateHz;

        // Pre-warp the cutoff frequency for bilinear transform
        double wc = 2 * Math.PI * cutoffHz / sampleRateHz;
        double k = Math.Tan(wc / 2);
        double k2 = k * k;
        double sqrt2 = Math.Sqrt(2);

        // Bilinear transform coefficients for 2nd-order Butterworth
        double norm = 1 / (1 + sqrt2 * k + k2);

        _b0 = k2 * norm;
        _b1 = 2 * k2 * norm;
        _b2 = k2 * norm;

        _a0 = 1;
        _a1 = 2 * (k2 - 1) * norm;
        _a2 = (1 - sqrt2 * k + k2) * norm;
    }

    /// <summary>
    /// Process a single sample through the filter.
    /// </summary>
    /// <param name="input">Input sample</param>
    /// <returns>Filtered output sample</returns>
    public float Process(float input)
    {
        // Direct Form II Transposed implementation
        double output = _b0 * input + _b1 * _x1 + _b2 * _x2
                      - _a1 * _y1 - _a2 * _y2;

        // Update state
        _x2 = _x1;
        _x1 = input;
        _y2 = _y1;
        _y1 = output;

        return (float)output;
    }

    /// <summary>
    /// Reset filter state (clears history).
    /// </summary>
    public void Reset()
    {
        _x1 = _x2 = 0;
        _y1 = _y2 = 0;
    }
}

/// <summary>
/// Separates CoP signal from vibration noise using low-pass filtering.
/// </summary>
public class SignalSeparator
{
    private readonly ButterworthFilter _filterX;
    private readonly ButterworthFilter _filterY;

    /// <summary>
    /// Whether filtering is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Cutoff frequency in Hz.
    /// </summary>
    public float CutoffHz { get; }

    /// <summary>
    /// Creates a signal separator with the specified cutoff frequency.
    /// </summary>
    /// <param name="cutoffHz">Cutoff frequency in Hz (default: 6Hz for Bass Shaker separation)</param>
    /// <param name="sampleRateHz">Sample rate in Hz (default: 60Hz for WBB)</param>
    public SignalSeparator(float cutoffHz = 6f, float sampleRateHz = 60f)
    {
        CutoffHz = cutoffHz;
        _filterX = new ButterworthFilter(cutoffHz, sampleRateHz);
        _filterY = new ButterworthFilter(cutoffHz, sampleRateHz);
    }

    /// <summary>
    /// Process a CoP sample.
    /// </summary>
    /// <param name="rawX">Raw X coordinate (mm)</param>
    /// <param name="rawY">Raw Y coordinate (mm)</param>
    /// <returns>Filtered coordinates (mm)</returns>
    public (float X, float Y) Process(float rawX, float rawY)
    {
        if (!IsEnabled)
            return (rawX, rawY);

        return (_filterX.Process(rawX), _filterY.Process(rawY));
    }

    /// <summary>
    /// Process a CoP sample.
    /// </summary>
    public System.Numerics.Vector2 Process(System.Numerics.Vector2 raw)
    {
        var (x, y) = Process(raw.X, raw.Y);
        return new System.Numerics.Vector2(x, y);
    }

    /// <summary>
    /// Reset filter state.
    /// </summary>
    public void Reset()
    {
        _filterX.Reset();
        _filterY.Reset();
    }
}
