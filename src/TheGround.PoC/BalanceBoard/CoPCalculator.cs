using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheGround.PoC.BalanceBoard;

/// <summary>
/// Calculates Center of Pressure (CoP) from Wii Balance Board sensor values.
/// Implements clinical posturography standards (ISPGR recommendations).
/// </summary>
public class CoPCalculator
{
    // Wii Balance Board dimensions - distance between sensors
    private const float BoardLengthMm = 433f;  // Front-back (Y axis)
    private const float BoardWidthMm = 238f;   // Left-right (X axis)

    // Clinical calibration: 3 seconds at 60Hz = 180 samples
    private const int CalibrationSamples = 180;
    private const float MinWeightForCalibration = 5f;  // kg

    private Vector2 _calibrationOffset = Vector2.Zero;
    private float _tareWeight = 0f;
    private bool _isCalibrated = false;
    
    // Calibration averaging
    private List<Vector2> _calibrationBuffer = new();
    private List<float> _calibrationWeightBuffer = new();
    private bool _isCalibrating = false;
    
    public event Action<int, int>? OnCalibrationProgress;  // current, total
    public event Action<bool>? OnCalibrationComplete;  // success

    /// <summary>
    /// Whether calibration has been performed.
    /// </summary>
    public bool IsCalibrated => _isCalibrated;
    
    /// <summary>
    /// Whether calibration is in progress.
    /// </summary>
    public bool IsCalibrating => _isCalibrating;

    /// <summary>
    /// Current calibration offset.
    /// </summary>
    public Vector2 CalibrationOffset => _calibrationOffset;

    /// <summary>
    /// Current tare weight.
    /// </summary>
    public float TareWeight => _tareWeight;

    /// <summary>
    /// Calculate raw CoP position in mm from sensor values.
    /// Optimized with precomputed constants.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Vector2 CalculateRaw(float tl, float tr, float bl, float br, out float totalWeight)
    {
        totalWeight = tl + tr + bl + br;

        // Minimum weight threshold for valid measurement (reduces noise when stepping off)
        if (totalWeight < MinWeightForCalibration)
            return Vector2.Zero;

        // Precomputed constants for speed
        const float HalfWidth = BoardWidthMm * 0.5f;
        const float HalfLength = BoardLengthMm * 0.5f;
        
        float invWeight = 1f / totalWeight;
        
        // X positive = right, Y positive = forward
        float copX = HalfWidth * ((tr + br) - (tl + bl)) * invWeight;
        float copY = HalfLength * ((tl + tr) - (bl + br)) * invWeight;

        return new Vector2(copX, copY);
    }

    /// <summary>
    /// Calculate calibrated CoP position.
    /// </summary>
    public Vector2 Calculate(float tl, float tr, float bl, float br, out float netWeight)
    {
        var rawCoP = CalculateRaw(tl, tr, bl, br, out float totalWeight);
        netWeight = totalWeight - _tareWeight;

        // Add to calibration buffer if calibrating
        if (_isCalibrating && totalWeight > MinWeightForCalibration)
        {
            _calibrationBuffer.Add(rawCoP);
            _calibrationWeightBuffer.Add(totalWeight);
            OnCalibrationProgress?.Invoke(_calibrationBuffer.Count, CalibrationSamples);
            
            if (_calibrationBuffer.Count >= CalibrationSamples)
            {
                CompleteCalibration();
            }
        }

        if (_isCalibrated)
        {
            return rawCoP - _calibrationOffset;
        }

        return rawCoP;
    }

    /// <summary>
    /// Calculate calibrated CoP from a sensor reading.
    /// </summary>
    public Vector2 Calculate(WiiBalanceBoardReader.SensorReading reading, out float netWeight)
    {
        return Calculate(
            reading.TopLeft,
            reading.TopRight,
            reading.BottomLeft,
            reading.BottomRight,
            out netWeight
        );
    }

    /// <summary>
    /// Start averaging calibration (3 seconds of samples).
    /// Stand still on the board while calibrating.
    /// </summary>
    public void StartCalibration()
    {
        _calibrationBuffer.Clear();
        _calibrationWeightBuffer.Clear();
        _isCalibrating = true;
    }
    
    private void CompleteCalibration()
    {
        _isCalibrating = false;
        
        if (_calibrationBuffer.Count < 10)
        {
            OnCalibrationComplete?.Invoke(false);
            return;
        }
        
        // Calculate average CoP
        Vector2 sumCoP = Vector2.Zero;
        float sumWeight = 0f;
        
        foreach (var cop in _calibrationBuffer)
            sumCoP += cop;
        foreach (var w in _calibrationWeightBuffer)
            sumWeight += w;
        
        _calibrationOffset = sumCoP / _calibrationBuffer.Count;
        _tareWeight = sumWeight / _calibrationWeightBuffer.Count;
        _isCalibrated = true;
        
        _calibrationBuffer.Clear();
        _calibrationWeightBuffer.Clear();
        
        OnCalibrationComplete?.Invoke(true);
    }

    /// <summary>
    /// Cancel ongoing calibration.
    /// </summary>
    public void CancelCalibration()
    {
        _isCalibrating = false;
        _calibrationBuffer.Clear();
        _calibrationWeightBuffer.Clear();
    }

    /// <summary>
    /// Reset calibration to default.
    /// </summary>
    public void ResetCalibration()
    {
        _calibrationOffset = Vector2.Zero;
        _tareWeight = 0f;
        _isCalibrated = false;
        _isCalibrating = false;
        _calibrationBuffer.Clear();
        _calibrationWeightBuffer.Clear();
    }
}

