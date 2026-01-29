using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Simple ski jump game controller.
/// Attach to an empty GameObject in your scene.
/// </summary>
public class SkiJumpController : MonoBehaviour
{
    #region Inspector Settings
    [Header("═══ Physics ═══")]
    [SerializeField] private float _maxSpeed = 25f;           // m/s (90 km/h)
    [SerializeField] private float _gravity = 9.8f;
    [SerializeField] private float _slopeAngle = 36f;         // degrees
    [SerializeField] private float _airResistance = 0.01f;
    
    [Header("═══ Posture Control ═══")]
    [SerializeField] private float _deadZoneMm = 15f;
    [SerializeField] private float _maxLeanMm = 50f;
    [SerializeField] private float _forwardDragCoeff = 0.3f;  // Aerodynamic tuck
    [SerializeField] private float _neutralDragCoeff = 0.5f;
    [SerializeField] private float _backwardDragCoeff = 0.8f;
    
    [Header("═══ Timing ═══")]
    [SerializeField] private float _countdownDuration = 3f;
    [SerializeField] private float _runDuration = 10f;
    [SerializeField] private float _flightMaxDuration = 5f;
    [SerializeField] private float _resultDisplayDuration = 5f;
    
    [Header("═══ Takeoff ═══")]
    [SerializeField] private float _takeoffPosition = 95f;    // meters from start
    [SerializeField] private float _kPoint = 90f;             // meters for scoring
    
    [Header("═══ References ═══")]
    [SerializeField] private Transform _player;
    [SerializeField] private Transform _takeoffEdge;
    #endregion
    
    #region State
    public enum GameState { Waiting, Countdown, Running, InAir, Landing, Result }
    public GameState CurrentState { get; private set; } = GameState.Waiting;
    
    // Physics state
    public float CurrentSpeed { get; private set; }           // m/s
    public float NormalizedSpeed => CurrentSpeed / _maxSpeed;
    public float TraveledDistance { get; private set; }       // meters
    public float JumpDistance { get; private set; }           // meters
    public float TakeoffSpeed { get; private set; }           // m/s at takeoff
    
    // Timing
    private float _stateTimer;
    private float _countdownValue;
    
    // Events
    public event System.Action OnCountdownStarted;
    public event System.Action<int> OnCountdownTick;          // 3, 2, 1
    public event System.Action OnRunStarted;
    public event System.Action OnTakeoff;
    public event System.Action<float> OnLanded;               // jump distance
    public event System.Action OnResultShown;
    #endregion
    
    #region Unity Lifecycle
    void Update()
    {
        switch (CurrentState)
        {
            case GameState.Waiting:
                // Wait for StartGame() call
                break;
                
            case GameState.Countdown:
                UpdateCountdown();
                break;
                
            case GameState.Running:
                UpdateRunning();
                break;
                
            case GameState.InAir:
                UpdateFlight();
                break;
                
            case GameState.Landing:
                UpdateLanding();
                break;
                
            case GameState.Result:
                UpdateResult();
                break;
        }
    }
    #endregion
    
    #region Public API
    /// <summary>Start a new game from countdown.</summary>
    public void StartGame()
    {
        if (CurrentState != GameState.Waiting && CurrentState != GameState.Result) return;
        
        CurrentSpeed = 0;
        TraveledDistance = 0;
        JumpDistance = 0;
        _stateTimer = 0;
        _countdownValue = _countdownDuration;
        
        CurrentState = GameState.Countdown;
        OnCountdownStarted?.Invoke();
    }
    
    /// <summary>Return to title scene.</summary>
    public void ReturnToTitle()
    {
        TheGroundManager.Instance?.StopVibration();
        SceneManager.LoadScene("TitleScene");
    }
    
    /// <summary>Retry the jump.</summary>
    public void Retry()
    {
        CurrentState = GameState.Waiting;
        StartGame();
    }
    #endregion
    
    #region State Updates
    private void UpdateCountdown()
    {
        _stateTimer += Time.deltaTime;
        
        int prevSecond = Mathf.CeilToInt(_countdownValue);
        _countdownValue -= Time.deltaTime;
        int currentSecond = Mathf.CeilToInt(_countdownValue);
        
        if (currentSecond != prevSecond && currentSecond > 0)
        {
            OnCountdownTick?.Invoke(currentSecond);
        }
        
        if (_countdownValue <= 0)
        {
            // Start running
            CurrentState = GameState.Running;
            _stateTimer = 0;
            OnRunStarted?.Invoke();
            
            // Start snow vibration
            TheGroundManager.Instance?.StartSnowVibration(0.3f);
        }
    }
    
    private void UpdateRunning()
    {
        _stateTimer += Time.deltaTime;
        
        // Get posture from Balance Board
        Vector2 copMm = GetCoPPosition();
        float dragCoeff = CalculateDragCoefficient(copMm.y);
        
        // Physics: acceleration = g * sin(angle) - drag * v^2
        float slopeAccel = _gravity * Mathf.Sin(_slopeAngle * Mathf.Deg2Rad);
        float dragDecel = dragCoeff * _airResistance * CurrentSpeed * CurrentSpeed;
        float acceleration = slopeAccel - dragDecel;
        
        CurrentSpeed += acceleration * Time.deltaTime;
        CurrentSpeed = Mathf.Clamp(CurrentSpeed, 0, _maxSpeed);
        
        TraveledDistance += CurrentSpeed * Time.deltaTime;
        
        // Update vibration velocity
        TheGroundManager.Instance?.UpdateVelocity(NormalizedSpeed);
        
        // Move player
        if (_player != null)
        {
            _player.position += Vector3.forward * CurrentSpeed * Time.deltaTime;
        }
        
        // Check takeoff
        if (TraveledDistance >= _takeoffPosition || 
            (_takeoffEdge != null && _player != null && _player.position.z >= _takeoffEdge.position.z))
        {
            EnterFlightPhase();
        }
    }
    
    private void UpdateFlight()
    {
        _stateTimer += Time.deltaTime;
        
        // Calculate jump distance based on speed and posture
        Vector2 copMm = GetCoPPosition();
        
        // Forward lean extends jump
        float leanFactor = Mathf.Clamp01((copMm.y + _maxLeanMm) / (2 * _maxLeanMm));
        float distancePerSecond = TakeoffSpeed * 0.3f * leanFactor;
        
        JumpDistance += distancePerSecond * Time.deltaTime;
        
        // Simulate descent
        if (_player != null)
        {
            _player.position += new Vector3(0, -_gravity * Time.deltaTime * 0.5f, CurrentSpeed * Time.deltaTime * 0.5f);
        }
        
        // Landing check (simplified: time-based or height-based)
        if (_stateTimer >= _flightMaxDuration)
        {
            Land();
        }
    }
    
    private void UpdateLanding()
    {
        _stateTimer += Time.deltaTime;
        
        if (_stateTimer >= 1f)
        {
            CurrentState = GameState.Result;
            _stateTimer = 0;
            OnResultShown?.Invoke();
        }
    }
    
    private void UpdateResult()
    {
        _stateTimer += Time.deltaTime;
        
        // Auto return after timeout (optional)
    }
    
    private void EnterFlightPhase()
    {
        TakeoffSpeed = CurrentSpeed;
        JumpDistance = 0;
        CurrentState = GameState.InAir;
        _stateTimer = 0;
        
        // ★ STOP VIBRATION IMMEDIATELY ★
        TheGroundManager.Instance?.StopVibration();
        
        OnTakeoff?.Invoke();
        Debug.Log($"[SkiJump] Takeoff! Speed: {TakeoffSpeed * 3.6f:F1} km/h");
    }
    
    private void Land()
    {
        CurrentState = GameState.Landing;
        _stateTimer = 0;
        
        // Landing impact pulse
        TheGroundManager.Instance?.PulseVibration(0.2f, 1f);
        
        OnLanded?.Invoke(JumpDistance);
        Debug.Log($"[SkiJump] Landed! Distance: {JumpDistance:F1}m");
    }
    #endregion
    
    #region Helpers
    private Vector2 GetCoPPosition()
    {
        if (TheGroundManager.Instance != null && TheGroundManager.Instance.IsUserOnBoard)
        {
            return TheGroundManager.Instance.CoPPositionMm;
        }
        return Vector2.zero;
    }
    
    private float CalculateDragCoefficient(float copY)
    {
        if (copY > _deadZoneMm)
        {
            // Forward lean = less drag
            float t = Mathf.Clamp01((copY - _deadZoneMm) / (_maxLeanMm - _deadZoneMm));
            return Mathf.Lerp(_neutralDragCoeff, _forwardDragCoeff, t);
        }
        else if (copY < -_deadZoneMm)
        {
            // Backward lean = more drag
            float t = Mathf.Clamp01((-copY - _deadZoneMm) / (_maxLeanMm - _deadZoneMm));
            return Mathf.Lerp(_neutralDragCoeff, _backwardDragCoeff, t);
        }
        return _neutralDragCoeff;
    }
    
    /// <summary>Calculate star rating based on K-point.</summary>
    public int GetStarRating()
    {
        float ratio = JumpDistance / _kPoint;
        if (ratio >= 1.1f) return 5;
        if (ratio >= 1.0f) return 4;
        if (ratio >= 0.9f) return 3;
        if (ratio >= 0.8f) return 2;
        return 1;
    }
    #endregion
}
