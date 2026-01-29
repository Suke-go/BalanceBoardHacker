using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace TheGround.Core
{
    /// <summary>
    /// UDP packet structure for CoP data transmission.
    /// Fixed 32-byte packet for efficient network transfer.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CoPPacket
    {
        /// <summary>Magic header "TGND"</summary>
        public uint Header;  // 0x444E4754 = "TGND"
        
        /// <summary>Protocol version</summary>
        public byte Version;
        
        /// <summary>Status flags</summary>
        public byte Flags;
        // bit 0: IsValid
        // bit 1: IsCalibrated
        // bit 2: IsConverged
        // bit 3: VibrationActive
        
        /// <summary>Reserved for future use</summary>
        public ushort Reserved;
        
        /// <summary>CoP X position in mm</summary>
        public float CopX;
        
        /// <summary>CoP Y position in mm</summary>
        public float CopY;
        
        /// <summary>Weight in kg</summary>
        public float Weight;
        
        /// <summary>SNR improvement in dB</summary>
        public float Snr;
        
        /// <summary>Timestamp (Unix milliseconds)</summary>
        public long Timestamp;
        
        public const uint MagicHeader = 0x444E4754;  // "TGND" in little-endian
        public const byte CurrentVersion = 1;
        public const int PacketSize = 32;
        
        public bool IsValid => (Flags & 0x01) != 0;
        public bool IsCalibrated => (Flags & 0x02) != 0;
        public bool IsConverged => (Flags & 0x04) != 0;
        public bool VibrationActive => (Flags & 0x08) != 0;
        
        /// <summary>
        /// Create packet from CoPResult.
        /// </summary>
        public static CoPPacket FromResult(CoPResult result, bool vibrationActive, float snr)
        {
            byte flags = 0;
            if (result.IsValid) flags |= 0x01;
            // IsCalibrated and IsConverged should be set by caller
            if (vibrationActive) flags |= 0x08;
            
            return new CoPPacket
            {
                Header = MagicHeader,
                Version = CurrentVersion,
                Flags = flags,
                Reserved = 0,
                CopX = result.X,
                CopY = result.Y,
                Weight = result.Weight,
                Snr = snr,
                Timestamp = result.TimestampMs
            };
        }
        
        /// <summary>
        /// Serialize to byte array.
        /// </summary>
        public byte[] ToBytes()
        {
            byte[] bytes = new byte[PacketSize];
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                Marshal.StructureToPtr(this, handle.AddrOfPinnedObject(), false);
            }
            finally
            {
                handle.Free();
            }
            return bytes;
        }
        
        /// <summary>
        /// Deserialize from byte array.
        /// </summary>
        public static CoPPacket FromBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length < PacketSize)
                throw new ArgumentException("Invalid packet size");
            
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                return Marshal.PtrToStructure<CoPPacket>(handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }
        }
        
        /// <summary>
        /// Validate packet header.
        /// </summary>
        public bool ValidateHeader() => Header == MagicHeader && Version == CurrentVersion;
    }
    
    /// <summary>
    /// UDP sender for transmitting CoP data.
    /// </summary>
    public class CoPSender : IDisposable
    {
        private readonly UdpClient _client;
        private readonly IPEndPoint _endpoint;
        private bool _disposed;
        
        public CoPSender(string host = "127.0.0.1", int port = 9000)
        {
            _endpoint = new IPEndPoint(IPAddress.Parse(host), port);
            _client = new UdpClient();
        }
        
        /// <summary>
        /// Send CoP packet.
        /// </summary>
        public void Send(CoPPacket packet)
        {
            if (_disposed) return;
            byte[] data = packet.ToBytes();
            _client.Send(data, data.Length, _endpoint);
        }
        
        /// <summary>
        /// Send CoP result with additional status.
        /// </summary>
        public void Send(CoPResult result, bool isCalibrated, bool isConverged, bool vibrationActive, float snr)
        {
            byte flags = 0;
            if (result.IsValid) flags |= 0x01;
            if (isCalibrated) flags |= 0x02;
            if (isConverged) flags |= 0x04;
            if (vibrationActive) flags |= 0x08;
            
            var packet = new CoPPacket
            {
                Header = CoPPacket.MagicHeader,
                Version = CoPPacket.CurrentVersion,
                Flags = flags,
                Reserved = 0,
                CopX = result.X,
                CopY = result.Y,
                Weight = result.Weight,
                Snr = snr,
                Timestamp = result.TimestampMs
            };
            
            Send(packet);
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
    
    /// <summary>
    /// UDP receiver for receiving CoP data (for Unity/clients).
    /// </summary>
    public class CoPReceiver : IDisposable
    {
        private readonly UdpClient _client;
        private bool _disposed;
        private IPEndPoint _remoteEP;
        
        public event Action<CoPPacket>? OnPacketReceived;
        
        public CoPReceiver(int port = 9000)
        {
            _client = new UdpClient(port);
            _remoteEP = new IPEndPoint(IPAddress.Any, 0);
        }
        
        /// <summary>
        /// Try to receive a packet (non-blocking if data available).
        /// </summary>
        public bool TryReceive(out CoPPacket packet)
        {
            packet = default;
            if (_disposed || _client.Available < CoPPacket.PacketSize)
                return false;
            
            try
            {
                byte[] data = _client.Receive(ref _remoteEP);
                if (data.Length >= CoPPacket.PacketSize)
                {
                    packet = CoPPacket.FromBytes(data);
                    if (packet.ValidateHeader())
                    {
                        OnPacketReceived?.Invoke(packet);
                        return true;
                    }
                }
            }
            catch
            {
                // Ignore receive errors
            }
            
            return false;
        }
        
        /// <summary>
        /// Receive packet (blocking).
        /// </summary>
        public CoPPacket Receive()
        {
            byte[] data = _client.Receive(ref _remoteEP);
            return CoPPacket.FromBytes(data);
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
}
