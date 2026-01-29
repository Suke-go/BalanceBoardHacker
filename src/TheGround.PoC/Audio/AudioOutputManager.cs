using System;
using System.Collections.Generic;
using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace TheGround.PoC.Audio;

/// <summary>
/// Manages audio output via WASAPI for low-latency haptic feedback.
/// </summary>
public class AudioOutputManager : IDisposable
{
    private WasapiOut? _wasapiOut;
    private readonly SineWaveGenerator _generator;
    private bool _disposed;

    public AudioOutputManager()
    {
        _generator = new SineWaveGenerator();
    }

    /// <summary>
    /// The sine wave generator used for output.
    /// </summary>
    public SineWaveGenerator Generator => _generator;

    /// <summary>
    /// Whether audio output is currently active.
    /// </summary>
    public bool IsPlaying => _wasapiOut?.PlaybackState == PlaybackState.Playing;

    /// <summary>
    /// Get list of available audio output devices.
    /// </summary>
    public static List<DeviceInfo> GetOutputDevices()
    {
        var devices = new List<DeviceInfo>();
        var enumerator = new MMDeviceEnumerator();

        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            devices.Add(new DeviceInfo
            {
                Id = device.ID,
                Name = device.FriendlyName,
                IsDefault = device.ID == enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID
            });
        }

        return devices;
    }

    /// <summary>
    /// Initialize audio output with the specified device.
    /// </summary>
    /// <param name="deviceId">Device ID (null for default device)</param>
    /// <param name="latencyMs">Target latency in milliseconds</param>
    public void Initialize(string? deviceId = null, int latencyMs = 50)
    {
        Stop();

        var enumerator = new MMDeviceEnumerator();
        MMDevice device;

        if (string.IsNullOrEmpty(deviceId))
        {
            device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
        else
        {
            device = enumerator.GetDevice(deviceId);
        }

        // Use shared mode for compatibility, exclusive mode for lowest latency
        _wasapiOut = new WasapiOut(device, AudioClientShareMode.Shared, false, latencyMs);
        _wasapiOut.Init(_generator);
    }

    /// <summary>
    /// Start audio playback.
    /// </summary>
    public void Play()
    {
        if (_wasapiOut == null)
            throw new InvalidOperationException("Audio not initialized. Call Initialize() first.");

        _generator.IsPlaying = true;
        _wasapiOut.Play();
    }

    /// <summary>
    /// Stop audio playback.
    /// </summary>
    public void Stop()
    {
        _generator.IsPlaying = false;

        if (_wasapiOut != null)
        {
            _wasapiOut.Stop();
            _wasapiOut.Dispose();
            _wasapiOut = null;
        }

        _generator.Reset();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Information about an audio device.
    /// </summary>
    public class DeviceInfo
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public bool IsDefault { get; init; }

        public override string ToString() => IsDefault ? $"{Name} (Default)" : Name;
    }
}
