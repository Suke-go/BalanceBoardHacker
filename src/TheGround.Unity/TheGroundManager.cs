using UnityEngine;
using UnityEngine.Events;
using TheGround.Core;
using System;

/// <summary>
/// Unity component for receiving CoP data and providing locomotion/haptic control.
/// Designed for Inspector-friendly workflow with UnityEvents.
/// </summary>
public class TheGroundManager : MonoBehaviour
{
    #region Singleton
    public static TheGroundManager Instance { get; private set; }
    #endregion
    
    #region Inspector - Connection
    [Header("═══ Connection ═══")]
    [Tooltip("UDP port to receive CoP data")]
    [SerializeField] private int _udpPort = 9000;
    
    [Tooltip("Auto-connect on Start")]
    [SerializeField] private bool _autoConnect = true;
    #endregion
    
    #region Inspector - Visualization
    [Header("═══ Visualization ═══")]
    [Tooltip("Transform to move based on CoP (optional)")]
    [SerializeField] private Transform _copMarker;
    
    [Tooltip("Scale: mm to Unity units")]
    [SerializeField] private float _positionScale = 0.01f;
    
    [Tooltip("Smoothing factor for position updates")]
    [Range(1f, 30f)]
    [SerializeField] private float _smoothing = 10f;
    #endregion
    
    #region Inspector - Locomotion / Lean Detection
    [Header("═══ Locomotion (Lean Detection) ═══")]
    [Tooltip("Enable lean-based movement detection")]
    [SerializeField] private bool _enableLocomotion = true;
    
    [Tooltip("Dead zone radius in mm (no movement inside)")]
    [SerializeField] private float _deadZoneMm = 20f;
    
    [Tooltip("Max lean distance in mm (full speed)")]
    [SerializeField] private float _maxLeanMm = 80f;
    
    [Tooltip("Forward direction threshold (0-1, 1=forward only)")]
    [Range(0f, 1f)]
    [SerializeField] private float _forwardBias = 0.3f;
    
    [Tooltip("Invert Y axis (backward = forward)")]
    [SerializeField] private bool _invertY = false;
    #endregion
    
    #region Inspector - Haptic Control
    [Header("═══ Haptic Control ═══")]
    [Tooltip("Target host for sending haptic commands")]
    [SerializeField] private string _hapticHost = "127.0.0.1";
    
    [Tooltip("UDP port for haptic commands")]
    [SerializeField] private int _hapticPort = 9001;
    
    [Tooltip("Default vibration frequency (Hz)")]
    [Range(10f, 80f)]
    [SerializeField] private float _defaultFrequency = 30f;
    
    [Tooltip("Default vibration amplitude (0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float _defaultAmplitude = 0.5f;
    #endregion
    
    #region Inspector - Calibration
    [Header("═══ Calibration ═══")]
    [Tooltip("Calibration duration in seconds")]
    [SerializeField] private float _calibrationDuration = 3f;
    
    [Tooltip("Show calibration UI overlay")]
    [SerializeField] private bool _showCalibrationUI = true;
    
    [Tooltip("Calibration complete sound (optional)")]
    [SerializeField] private AudioClip _calibrationCompleteSound;
    #endregion
    
    #region Inspector - Events
    [Header("═══ Events ═══")]
    [Tooltip("Called when CoP data is updated")]
    public UnityEvent<Vector2> OnCoPUpdated;
    
    [Tooltip("Called when calibration completes")]
    public UnityEvent OnCalibrationComplete;
    
    [Tooltip("Called when compensation converges")]
    public UnityEvent<float> OnCompensationConverged;
    
    [Tooltip("Called when lean-based movement is detected")]
    public UnityEvent<Vector2> OnLocomotionInput;
    
    [Tooltip("Called when user steps off the board")]
    public UnityEvent OnUserSteppedOff;
    
    [Tooltip("Called when user steps on the board")]
    public UnityEvent OnUserSteppedOn;
    #endregion
    
    #region Inspector - Debug
    [Header("═══ Debug ═══")]
    [SerializeField] private bool _showDebugGUI = true;
    [SerializeField] private bool _logEvents = false;
    #endregion
    
    #region Public Properties
    /// <summary>Current CoP position in mm (X=right, Y=forward)</summary>
    public Vector2 CoPPositionMm => new Vector2(_lastPacket.CopX, _lastPacket.CopY);
    
    /// <summary>Current CoP position in Unity units</summary>
    public Vector3 CoPPositionUnity => new Vector3(_lastPacket.CopX * _positionScale, 0, _lastPacket.CopY * _positionScale);
    
    /// <summary>Current weight in kg</summary>
    public float Weight => _lastPacket.Weight;
    
    /// <summary>Is there a valid reading (user on board)?</summary>
    public bool IsUserOnBoard => _lastPacket.IsValid;
    
    /// <summary>Is the system calibrated?</summary>
    public bool IsCalibrated => _lastPacket.IsCalibrated;
    
    /// <summary>Is compensation converged?</summary>
    public bool IsConverged => _lastPacket.IsConverged;
    
    /// <summary>SNR improvement in dB</summary>
    public float SnrImprovement => _lastPacket.Snr;
    
    /// <summary>Normalized locomotion input (-1 to 1)</summary>
    public Vector2 LocomotionInput { get; private set; }
    
    /// <summary>Is vibration currently active?</summary>
    public bool IsVibrating { get; private set; }
    
    /// <summary>Current vibration frequency</summary>
    public float VibrationFrequency => _currentFrequency;
    
    /// <summary>Current vibration amplitude</summary>
    public float VibrationAmplitude => _currentAmplitude;
    #endregion
    
    #region Private State
    private CoPReceiver _receiver;
    private CoPPacket _lastPacket;
    private Vector3 _smoothedPosition;
    private bool _wasOnBoard = false;
    private bool _wasCalibrated = false;
    private bool _wasConverged = false;
    private AudioSource _audioSource;
    
    // Haptic state
    private float _currentFrequency;
    private float _currentAmplitude;
    private System.Net.Sockets.UdpClient _hapticSender;
    private float _lastVelocitySendTime;
    #endregion
    
    #region Unity Lifecycle
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null && _calibrationCompleteSound != null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _currentFrequency = _defaultFrequency;
        _currentAmplitude = _defaultAmplitude;
    }
    
    void Start()
    {
        if (_autoConnect)
        {
            Connect();
        }
    }
    
    void Update()
    {
        if (_receiver == null) return;
        
        ProcessIncomingPackets();
        UpdateLocomotion();
        UpdateVisualization();
    }
    
    void OnDestroy()
    {
        Disconnect();
    }
    #endregion
    
    #region Public API - Connection
    /// <summary>Connect to UDP receiver.</summary>
    public void Connect()
    {
        if (_receiver != null) return;
        
        try
        {
            _receiver = new CoPReceiver(_udpPort);
            _hapticSender = new System.Net.Sockets.UdpClient();
            if (_logEvents) Debug.Log($"[TheGround] Connected on port {_udpPort}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TheGround] Connection failed: {ex.Message}");
        }
    }
    
    /// <summary>Disconnect from UDP receiver.</summary>
    public void Disconnect()
    {
        _receiver?.Dispose();
        _receiver = null;
        _hapticSender?.Dispose();
        _hapticSender = null;
    }
    #endregion
    
    #region Public API - Calibration
    /// <summary>Request calibration start (sent to PoC app).</summary>
    public void RequestCalibration()
    {
        SendHapticCommand("CAL_START");
        if (_logEvents) Debug.Log("[TheGround] Calibration requested");
    }
    
    /// <summary>Request compensation reset.</summary>
    public void RequestCompensationReset()
    {
        SendHapticCommand("COMP_RESET");
        if (_logEvents) Debug.Log("[TheGround] Compensation reset requested");
    }
    #endregion
    
    #region Public API - Vibration Control
    /// <summary>Start vibration with current settings.</summary>
    public void StartVibration()
    {
        StartVibration(_currentFrequency, _currentAmplitude);
    }
    
    /// <summary>Start vibration with specified parameters.</summary>
    public void StartVibration(float frequency, float amplitude)
    {
        _currentFrequency = Mathf.Clamp(frequency, 10f, 80f);
        _currentAmplitude = Mathf.Clamp01(amplitude);
        SendHapticCommand($"VIB_START,{_currentFrequency:F1},{_currentAmplitude:F2}");
        IsVibrating = true;
        if (_logEvents) Debug.Log($"[TheGround] Vibration started: {_currentFrequency}Hz @ {_currentAmplitude * 100}%");
    }
    
    /// <summary>Stop vibration.</summary>
    public void StopVibration()
    {
        SendHapticCommand("VIB_STOP");
        IsVibrating = false;
        if (_logEvents) Debug.Log("[TheGround] Vibration stopped");
    }
    
    /// <summary>Set vibration frequency (takes effect on next Start or immediately if playing).</summary>
    public void SetVibrationFrequency(float frequency)
    {
        _currentFrequency = Mathf.Clamp(frequency, 10f, 80f);
        if (IsVibrating)
        {
            SendHapticCommand($"VIB_FREQ,{_currentFrequency:F1}");
        }
    }
    
    /// <summary>Set vibration amplitude (takes effect on next Start or immediately if playing).</summary>
    public void SetVibrationAmplitude(float amplitude)
    {
        _currentAmplitude = Mathf.Clamp01(amplitude);
        if (IsVibrating)
        {
            SendHapticCommand($"VIB_AMP,{_currentAmplitude:F2}");
        }
    }
    
    /// <summary>Pulse vibration for a short duration.</summary>
    public void PulseVibration(float duration = 0.1f, float amplitude = 1f)
    {
        SendHapticCommand($"VIB_PULSE,{duration:F2},{amplitude:F2}");
        if (_logEvents) Debug.Log($"[TheGround] Vibration pulse: {duration}s @ {amplitude * 100}%");
    }
    
    /// <summary>Start snow texture vibration (ski simulation).</summary>
    public void StartSnowVibration(float amplitude = 0.5f)
    {
        _currentAmplitude = Mathf.Clamp01(amplitude);
        SendHapticCommand($"VIB_START,SNOW,{_currentAmplitude:F2}");
        IsVibrating = true;
        if (_logEvents) Debug.Log($"[TheGround] Snow vibration started @ {amplitude * 100}%");
    }
    
    /// <summary>Update velocity for snow texture (affects vibration character). Call at ~20Hz.</summary>
    public void UpdateVelocity(float normalizedVelocity)
    {
        // Rate limit to ~20Hz
        if (Time.time - _lastVelocitySendTime < 0.05f) return;
        _lastVelocitySendTime = Time.time;
        
        float v = Mathf.Clamp01(normalizedVelocity);
        SendHapticCommand($"VIB_VELOCITY,{v:F2}");
    }
    
    /// <summary>Send ping to keep connection alive.</summary>
    public void SendPing()
    {
        SendHapticCommand("PING");
    }
    #endregion
    
    #region Private Methods
    private void ProcessIncomingPackets()
    {
        while (_receiver.TryReceive(out var packet))
        {
            _lastPacket = packet;
            
            // Fire CoP update event
            OnCoPUpdated?.Invoke(new Vector2(packet.CopX, packet.CopY));
            
            // Detect step on/off
            if (packet.IsValid && !_wasOnBoard)
            {
                OnUserSteppedOn?.Invoke();
                if (_logEvents) Debug.Log("[TheGround] User stepped on");
            }
            else if (!packet.IsValid && _wasOnBoard)
            {
                OnUserSteppedOff?.Invoke();
                LocomotionInput = Vector2.zero;
                if (_logEvents) Debug.Log("[TheGround] User stepped off");
            }
            _wasOnBoard = packet.IsValid;
            
            // Detect calibration complete
            if (packet.IsCalibrated && !_wasCalibrated)
            {
                OnCalibrationComplete?.Invoke();
                if (_calibrationCompleteSound != null && _audioSource != null)
                {
                    _audioSource.PlayOneShot(_calibrationCompleteSound);
                }
                if (_logEvents) Debug.Log("[TheGround] Calibration complete");
            }
            _wasCalibrated = packet.IsCalibrated;
            
            // Detect convergence
            if (packet.IsConverged && !_wasConverged)
            {
                OnCompensationConverged?.Invoke(packet.Snr);
                if (_logEvents) Debug.Log($"[TheGround] Converged! SNR: {packet.Snr:F1} dB");
            }
            _wasConverged = packet.IsConverged;
        }
    }
    
    private void UpdateLocomotion()
    {
        if (!_enableLocomotion || !_lastPacket.IsValid)
        {
            LocomotionInput = Vector2.zero;
            return;
        }
        
        Vector2 cop = new Vector2(_lastPacket.CopX, _invertY ? -_lastPacket.CopY : _lastPacket.CopY);
        float distance = cop.magnitude;
        
        if (distance < _deadZoneMm)
        {
            LocomotionInput = Vector2.zero;
            return;
        }
        
        // Normalize to 0-1 range beyond dead zone
        float normalizedDistance = Mathf.Clamp01((distance - _deadZoneMm) / (_maxLeanMm - _deadZoneMm));
        Vector2 direction = cop.normalized;
        
        // Apply forward bias
        if (_forwardBias > 0)
        {
            float forwardComponent = Mathf.Max(0, direction.y);
            direction.y = Mathf.Lerp(direction.y, forwardComponent, _forwardBias);
            direction = direction.normalized;
        }
        
        LocomotionInput = direction * normalizedDistance;
        OnLocomotionInput?.Invoke(LocomotionInput);
    }
    
    private void UpdateVisualization()
    {
        if (_copMarker == null) return;
        
        Vector3 targetPos = new Vector3(
            _lastPacket.CopX * _positionScale,
            0f,
            _lastPacket.CopY * _positionScale
        );
        
        _smoothedPosition = Vector3.Lerp(_smoothedPosition, targetPos, _smoothing * Time.deltaTime);
        _copMarker.localPosition = _smoothedPosition;
    }
    
    private void SendHapticCommand(string command)
    {
        if (_hapticSender == null) return;
        
        try
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes(command);
            var endpoint = new System.Net.IPEndPoint(
                System.Net.IPAddress.Parse(_hapticHost), _hapticPort);
            _hapticSender.Send(data, data.Length, endpoint);
        }
        catch (Exception ex)
        {
            if (_logEvents) Debug.LogWarning($"[TheGround] Haptic command failed: {ex.Message}");
        }
    }
    #endregion
    
    #region Debug GUI
    void OnGUI()
    {
        if (!_showDebugGUI) return;
        
        var style = new GUIStyle(GUI.skin.box) { richText = true };
        GUILayout.BeginArea(new Rect(10, 10, 320, 220));
        GUILayout.BeginVertical(style);
        
        GUILayout.Label("<b>TheGround Status</b>");
        GUILayout.Space(5);
        
        // Status indicators
        string onBoard = _lastPacket.IsValid ? "<color=green>●</color>" : "<color=red>○</color>";
        string calibrated = _lastPacket.IsCalibrated ? "<color=green>●</color>" : "<color=yellow>○</color>";
        string converged = _lastPacket.IsConverged ? "<color=green>●</color>" : "<color=gray>○</color>";
        GUILayout.Label($"{onBoard} On Board  {calibrated} Calibrated  {converged} Converged", new GUIStyle(GUI.skin.label) { richText = true });
        
        GUILayout.Space(5);
        GUILayout.Label($"CoP: ({_lastPacket.CopX:F1}, {_lastPacket.CopY:F1}) mm");
        GUILayout.Label($"Weight: {_lastPacket.Weight:F1} kg");
        GUILayout.Label($"SNR: {_lastPacket.Snr:F1} dB");
        
        if (_enableLocomotion)
        {
            GUILayout.Space(5);
            GUILayout.Label($"Locomotion: ({LocomotionInput.x:F2}, {LocomotionInput.y:F2})");
        }
        
        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Calibrate")) RequestCalibration();
        if (GUILayout.Button(IsVibrating ? "Stop Vib" : "Start Vib"))
        {
            if (IsVibrating) StopVibration();
            else StartVibration();
        }
        GUILayout.EndHorizontal();
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
    #endregion
}
