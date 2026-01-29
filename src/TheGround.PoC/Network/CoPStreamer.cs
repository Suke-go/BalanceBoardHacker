using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace TheGround.PoC.Network;

/// <summary>
/// UDP packet structure for CoP data transmission (32 bytes).
/// Compatible with TheGround.Core.CoPPacket.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CoPPacket
{
    public uint Header;      // "TGND" = 0x444E4754
    public byte Version;     // Protocol version
    public byte Flags;       // bit0: Valid, bit1: Calibrated, bit2: Converged, bit3: Vibrating
    public ushort Reserved;
    public float CopX;       // mm
    public float CopY;       // mm
    public float Weight;     // kg
    public float Snr;        // dB
    public long Timestamp;   // Unix ms
    
    public const uint MagicHeader = 0x444E4754;
    public const byte CurrentVersion = 1;
    public const int Size = 32;
    
    public byte[] ToBytes()
    {
        byte[] bytes = new byte[Size];
        GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try { Marshal.StructureToPtr(this, handle.AddrOfPinnedObject(), false); }
        finally { handle.Free(); }
        return bytes;
    }
}

/// <summary>
/// UDP streamer for sending CoP data to Quest/clients.
/// </summary>
public class CoPStreamer : IDisposable
{
    private UdpClient? _client;
    private IPEndPoint _endpoint;
    private bool _isEnabled;
    private bool _disposed;
    
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            if (value && _client == null)
            {
                _client = new UdpClient();
            }
        }
    }
    
    public string TargetHost { get; set; } = "255.255.255.255";  // Broadcast by default
    public int TargetPort { get; set; } = 9000;
    
    public event Action<string>? OnStatusChanged;
    
    public CoPStreamer()
    {
        _endpoint = new IPEndPoint(IPAddress.Broadcast, 9000);
    }
    
    public void UpdateEndpoint()
    {
        try
        {
            _endpoint = new IPEndPoint(
                TargetHost == "255.255.255.255" ? IPAddress.Broadcast : IPAddress.Parse(TargetHost),
                TargetPort);
            
            if (_client != null && TargetHost == "255.255.255.255")
            {
                _client.EnableBroadcast = true;
            }
        }
        catch (Exception ex)
        {
            OnStatusChanged?.Invoke($"Invalid endpoint: {ex.Message}");
        }
    }
    
    public void Send(float copX, float copY, float weight, float snr,
                     bool isValid, bool isCalibrated, bool isConverged, bool isVibrating)
    {
        if (!_isEnabled || _client == null || _disposed) return;
        
        byte flags = 0;
        if (isValid) flags |= 0x01;
        if (isCalibrated) flags |= 0x02;
        if (isConverged) flags |= 0x04;
        if (isVibrating) flags |= 0x08;
        
        var packet = new CoPPacket
        {
            Header = CoPPacket.MagicHeader,
            Version = CoPPacket.CurrentVersion,
            Flags = flags,
            Reserved = 0,
            CopX = copX,
            CopY = copY,
            Weight = weight,
            Snr = snr,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        
        try
        {
            byte[] data = packet.ToBytes();
            _client.Send(data, data.Length, _endpoint);
        }
        catch
        {
            // Silently ignore send errors to avoid flooding logs
        }
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _client?.Dispose();
            _client = null;
        }
    }
}

/// <summary>
/// Simple command receiver for remote control.
/// </summary>
public class CommandReceiver : IDisposable
{
    private UdpClient? _client;
    private bool _disposed;
    private IPEndPoint _remoteEP;
    
    public int ListenPort { get; }
    public bool IsListening => _client != null;
    
    public event Action<string>? OnCommandReceived;
    
    public CommandReceiver(int port = 9001)
    {
        ListenPort = port;
        _remoteEP = new IPEndPoint(IPAddress.Any, 0);
    }
    
    public void Start()
    {
        if (_client != null) return;
        try
        {
            _client = new UdpClient(ListenPort);
        }
        catch { }
    }
    
    public void Stop()
    {
        _client?.Dispose();
        _client = null;
    }
    
    /// <summary>
    /// Try to receive a command (non-blocking).
    /// </summary>
    public bool TryReceive(out string command)
    {
        command = string.Empty;
        if (_client == null || _client.Available == 0) return false;
        
        try
        {
            byte[] data = _client.Receive(ref _remoteEP);
            command = Encoding.UTF8.GetString(data).Trim();
            OnCommandReceived?.Invoke(command);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public void Poll()
    {
        while (TryReceive(out string cmd))
        {
            // Event already fired in TryReceive
        }
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _client?.Dispose();
        }
    }
}

