using System;
using WiimoteLib;

namespace TheGround.PoC.BalanceBoard;

/// <summary>
/// Wrapper for WiimoteLib to read Wii Balance Board sensor data.
/// </summary>
public class WiiBalanceBoardReader : IDisposable
{
    private Wiimote? _wiimote;
    private bool _isConnected;
    private bool _disposed;

    /// <summary>
    /// Fired when new sensor data is received (approximately 60Hz).
    /// </summary>
    public event Action<SensorReading>? OnDataReceived;

    /// <summary>
    /// Fired when connection status changes.
    /// </summary>
    public event Action<string>? OnStatusChanged;

    /// <summary>
    /// Fired when an error occurs.
    /// </summary>
    public event Action<Exception>? OnError;

    /// <summary>
    /// Whether the balance board is currently connected.
    /// </summary>
    public bool IsConnected => _isConnected;

    /// <summary>
    /// Nominal sample rate of the Wii Balance Board.
    /// </summary>
    public float SampleRate => 60f;

    /// <summary>
    /// Attempts to connect to a Wii Balance Board.
    /// </summary>
    /// <returns>True if connection successful.</returns>
    public bool Connect()
    {
        try
        {
            OnStatusChanged?.Invoke("Searching for Wii Balance Board...");
            
            // First, try to find any Wiimotes/Balance Boards
            var collection = new WiimoteCollection();
            collection.FindAllWiimotes();
            
            if (collection.Count == 0)
            {
                OnStatusChanged?.Invoke("No Wiimotes/Balance Boards found in HID list. " +
                    "Please pair via Control Panel → Devices and Printers.");
                return false;
            }
            
            OnStatusChanged?.Invoke($"Found {collection.Count} device(s). Connecting...");
            
            // Connect to the first one (should be Balance Board)
            _wiimote = collection[0];
            _wiimote.WiimoteChanged += OnWiimoteChanged;
            _wiimote.WiimoteExtensionChanged += OnExtensionChanged;

            _wiimote.Connect();
            _wiimote.SetReportType(InputReport.IRExtensionAccel, true);
            _wiimote.SetLEDs(true, false, false, false);

            _isConnected = true;
            OnStatusChanged?.Invoke($"Connected! Extension: {_wiimote.WiimoteState.ExtensionType}");
            return true;
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex);
            OnStatusChanged?.Invoke($"Connection failed: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Gets a list of all connected Wiimote/Balance Board devices for debugging.
    /// </summary>
    public static int GetDeviceCount()
    {
        try
        {
            var collection = new WiimoteCollection();
            collection.FindAllWiimotes();
            return collection.Count;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Disconnects from the Wii Balance Board.
    /// </summary>
    public void Disconnect()
    {
        if (_wiimote != null)
        {
            _wiimote.WiimoteChanged -= OnWiimoteChanged;
            _wiimote.WiimoteExtensionChanged -= OnExtensionChanged;
            _wiimote.Disconnect();
            _wiimote = null;
        }

        _isConnected = false;
        OnStatusChanged?.Invoke("Disconnected");
    }

    private void OnWiimoteChanged(object? sender, WiimoteChangedEventArgs e)
    {
        if (e.WiimoteState.ExtensionType != ExtensionType.BalanceBoard)
            return;

        var bb = e.WiimoteState.BalanceBoardState;
        
        var reading = new SensorReading
        {
            TopLeft = bb.SensorValuesKg.TopLeft,
            TopRight = bb.SensorValuesKg.TopRight,
            BottomLeft = bb.SensorValuesKg.BottomLeft,
            BottomRight = bb.SensorValuesKg.BottomRight,
            CenterOfGravity = new System.Numerics.Vector2(
                bb.CenterOfGravity.X,
                bb.CenterOfGravity.Y
            ),
            Timestamp = DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond
        };

        OnDataReceived?.Invoke(reading);
    }

    private void OnExtensionChanged(object? sender, WiimoteExtensionChangedEventArgs e)
    {
        if (e.Inserted)
        {
            OnStatusChanged?.Invoke($"Extension connected: {e.ExtensionType}");
            if (e.ExtensionType == ExtensionType.BalanceBoard)
            {
                _wiimote?.SetReportType(InputReport.IRExtensionAccel, true);
            }
        }
        else
        {
            OnStatusChanged?.Invoke("Extension disconnected");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Disconnect();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Raw sensor reading from the Wii Balance Board.
    /// </summary>
    public struct SensorReading
    {
        /// <summary>Top-left sensor value in kg.</summary>
        public float TopLeft;
        /// <summary>Top-right sensor value in kg.</summary>
        public float TopRight;
        /// <summary>Bottom-left sensor value in kg.</summary>
        public float BottomLeft;
        /// <summary>Bottom-right sensor value in kg.</summary>
        public float BottomRight;
        /// <summary>Center of gravity from WiimoteLib (normalized -1 to 1).</summary>
        public System.Numerics.Vector2 CenterOfGravity;
        /// <summary>UTC timestamp in seconds.</summary>
        public double Timestamp;

        /// <summary>Total weight on the board in kg.</summary>
        public readonly float TotalWeight => TopLeft + TopRight + BottomLeft + BottomRight;
    }
}
