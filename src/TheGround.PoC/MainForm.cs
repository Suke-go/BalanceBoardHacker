using System;
using System.Drawing;
using System.Numerics;
using System.Windows.Forms;
using TheGround.PoC.Audio;
using TheGround.PoC.BalanceBoard;
using TheGround.PoC.Network;
using TheGround.PoC.SignalProcessing;

namespace TheGround.PoC;

public partial class MainForm : Form
{
    // Balance Board
    private readonly WiiBalanceBoardReader _boardReader;
    private readonly CoPCalculator _copCalculator;
    private readonly SignalSeparator _signalSeparator;
    private readonly BluetoothSetup _bluetoothSetup;
    private readonly AdaptiveVibrationCompensator _vibrationCompensator;

    // Audio
    private readonly AudioOutputManager _audioManager;

    // Network
    private readonly CoPStreamer _streamer;
    private readonly CommandReceiver _commandReceiver;
    private readonly HapticController _hapticController;
    private readonly System.Windows.Forms.Timer _commandPollTimer;

    // State
    private Vector2 _rawCoP;
    private Vector2 _filteredCoP;
    private Vector2 _compensatedCoP;  // After vibration compensation
    private float _totalWeight;
    private WiiBalanceBoardReader.SensorReading _lastReading;

    // Visualization
    private readonly object _lockObj = new();
    private const float VisualizationRangeMm = 150f;  // ±150mm range
    
    // UI throttling - 30 FPS max to prevent freezing
    private DateTime _lastUiUpdate = DateTime.MinValue;
    private const int UiUpdateIntervalMs = 33;  // ~30 FPS
    private volatile bool _uiUpdatePending = false;

    public MainForm()
    {
        InitializeComponent();

        // Enable double buffering for flicker-free drawing
        this.DoubleBuffered = true;
        typeof(Panel).InvokeMember("DoubleBuffered",
            System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null, panelCoP, new object[] { true });

        _boardReader = new WiiBalanceBoardReader();
        _copCalculator = new CoPCalculator();
        _signalSeparator = new SignalSeparator(cutoffHz: 6f, sampleRateHz: 60f);
        _audioManager = new AudioOutputManager();
        _bluetoothSetup = new BluetoothSetup();
        _vibrationCompensator = new AdaptiveVibrationCompensator(sampleRate: 60f);
        _streamer = new CoPStreamer();
        _commandReceiver = new CommandReceiver(9001);
        _hapticController = new HapticController(_audioManager);
        
        // Command polling timer (50ms = 20Hz)
        _commandPollTimer = new System.Windows.Forms.Timer { Interval = 50 };
        _commandPollTimer.Tick += OnCommandPoll;

        SetupEventHandlers();
        PopulateAudioDevices();
    }

    private void SetupEventHandlers()
    {
        _boardReader.OnDataReceived += OnBalanceBoardData;
        _boardReader.OnStatusChanged += status => 
            BeginInvoke(() => lblStatus.Text = status);
        _boardReader.OnError += ex => 
            BeginInvoke(() => MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error));
        _bluetoothSetup.OnStatusChanged += status =>
            BeginInvoke(() => lblStatus.Text = status);
        
        // Calibration events
        _copCalculator.OnCalibrationProgress += (current, total) =>
            BeginInvoke(() => lblStatus.Text = $"Calibrating... {current}/{total} samples ({current * 100 / total}%)");
        _copCalculator.OnCalibrationComplete += success =>
            BeginInvoke(() => {
                lblStatus.Text = success ? "Calibration complete!" : "Calibration failed - stay on the board";
                btnCalibrate.Text = "Calibrate";
                btnCalibrate.Enabled = true;
            });
    }

    private void PopulateAudioDevices()
    {
        cmbAudioDevice.Items.Clear();
        var devices = AudioOutputManager.GetOutputDevices();
        foreach (var device in devices)
        {
            cmbAudioDevice.Items.Add(device);
            if (device.IsDefault)
                cmbAudioDevice.SelectedItem = device;
        }
    }

    private void OnBalanceBoardData(WiiBalanceBoardReader.SensorReading reading)
    {
        lock (_lockObj)
        {
            _lastReading = reading;

            // Calculate CoP
            _rawCoP = _copCalculator.Calculate(reading, out _totalWeight);

            // Apply vibration compensation (synced with audio phase)
            bool isVibrating = _audioManager.IsPlaying;
            double audioPhase = isVibrating ? _audioManager.Generator.CurrentPhase : -1;
            _compensatedCoP = _vibrationCompensator.Process(_rawCoP, isVibrating, audioPhase);

            // Apply signal separation filter (low-pass)
            _filteredCoP = _signalSeparator.Process(_compensatedCoP);
            
            // Stream to Quest/clients
            _streamer.Send(
                _filteredCoP.X, _filteredCoP.Y, _totalWeight,
                _vibrationCompensator.SnrImprovement,
                _totalWeight > 5f,  // isValid
                _copCalculator.IsCalibrated,
                _vibrationCompensator.IsConverged,
                isVibrating);
        }

        // Throttle UI updates to prevent flooding (max 30 FPS)
        var now = DateTime.UtcNow;
        if (!_uiUpdatePending && (now - _lastUiUpdate).TotalMilliseconds >= UiUpdateIntervalMs)
        {
            _uiUpdatePending = true;
            _lastUiUpdate = now;
            BeginInvoke(() => {
                _uiUpdatePending = false;
                UpdateDisplay();
            });
        }
    }

    private void UpdateDisplay()
    {
        lock (_lockObj)
        {
            // Sensor values
            lblTL.Text = $"TL: {_lastReading.TopLeft:F2} kg";
            lblTR.Text = $"TR: {_lastReading.TopRight:F2} kg";
            lblBL.Text = $"BL: {_lastReading.BottomLeft:F2} kg";
            lblBR.Text = $"BR: {_lastReading.BottomRight:F2} kg";
            lblTotal.Text = $"Total: {_totalWeight:F2} kg";

            // CoP values
            lblRawCoP.Text = $"Raw: ({_rawCoP.X:F1}, {_rawCoP.Y:F1}) mm";
            lblFilteredCoP.Text = $"Filtered: ({_filteredCoP.X:F1}, {_filteredCoP.Y:F1}) mm";
        }

        // Redraw visualization panel
        panelCoP.Invalidate();
    }

    private void PanelCoP_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        int w = panelCoP.Width;
        int h = panelCoP.Height;
        int cx = w / 2;
        int cy = h / 2;

        // Background
        g.Clear(Color.White);

        // Grid lines
        using var gridPen = new Pen(Color.LightGray, 1);
        g.DrawLine(gridPen, cx, 0, cx, h);  // Vertical center
        g.DrawLine(gridPen, 0, cy, w, cy);  // Horizontal center

        // Grid circles
        for (int r = 50; r <= 150; r += 50)
        {
            float pixelRadius = r / VisualizationRangeMm * Math.Min(cx, cy);
            g.DrawEllipse(gridPen, cx - pixelRadius, cy - pixelRadius, pixelRadius * 2, pixelRadius * 2);
        }

        lock (_lockObj)
        {
            // Scale factor
            float scale = Math.Min(cx, cy) / VisualizationRangeMm;

            // Raw CoP (red)
            if (chkShowRaw.Checked)
            {
                float rawPx = cx + _rawCoP.X * scale;
                float rawPy = cy - _rawCoP.Y * scale;  // Y inverted for screen coords
                using var rawBrush = new SolidBrush(Color.FromArgb(180, Color.Red));
                g.FillEllipse(rawBrush, rawPx - 8, rawPy - 8, 16, 16);
            }

            // Filtered CoP (green)
            if (chkShowFiltered.Checked)
            {
                float filtPx = cx + _filteredCoP.X * scale;
                float filtPy = cy - _filteredCoP.Y * scale;
                using var filtBrush = new SolidBrush(Color.FromArgb(180, Color.Green));
                g.FillEllipse(filtBrush, filtPx - 8, filtPy - 8, 16, 16);
            }
        }

        // Labels
        using var font = new Font("Segoe UI", 8);
        g.DrawString("Front", font, Brushes.Gray, cx - 15, 5);
        g.DrawString("Back", font, Brushes.Gray, cx - 12, h - 18);
        g.DrawString("L", font, Brushes.Gray, 5, cy - 6);
        g.DrawString("R", font, Brushes.Gray, w - 15, cy - 6);
    }

    // === Button Handlers ===

    private void BtnConnect_Click(object? sender, EventArgs e)
    {
        if (_boardReader.IsConnected)
        {
            _boardReader.Disconnect();
            btnConnect.Text = "Connect";
        }
        else
        {
            if (_boardReader.Connect())
            {
                btnConnect.Text = "Disconnect";
            }
        }
    }

    private void BtnBluetoothSetup_Click(object? sender, EventArgs e)
    {
        btnBluetoothSetup.Enabled = false;
        lblStatus.Text = "Setting up Bluetooth HID...";
        
        // Run in background to not block UI
        Task.Run(() =>
        {
            _bluetoothSetup.SetupBalanceBoard();
            BeginInvoke(() => btnBluetoothSetup.Enabled = true);
        });
    }

    private void BtnCalibrate_Click(object? sender, EventArgs e)
    {
        if (_copCalculator.IsCalibrating)
        {
            _copCalculator.CancelCalibration();
            btnCalibrate.Text = "Calibrate";
            lblStatus.Text = "Calibration cancelled";
        }
        else
        {
            _signalSeparator.Reset();
            _copCalculator.StartCalibration();
            btnCalibrate.Text = "Cancel";
            lblStatus.Text = "Stand still for 3 seconds...";
        }
    }

    private void BtnPlayAudio_Click(object? sender, EventArgs e)
    {
        if (_audioManager.IsPlaying)
        {
            _audioManager.Stop();
            btnPlayAudio.Text = "▶ Play";
        }
        else
        {
            var selectedDevice = cmbAudioDevice.SelectedItem as AudioOutputManager.DeviceInfo;
            _audioManager.Initialize(selectedDevice?.Id, latencyMs: 50);
            _audioManager.Generator.Frequency = (float)numFrequency.Value;
            _audioManager.Generator.Amplitude = trkAmplitude.Value / 100f;
            _audioManager.Generator.EnableChannel1 = chkChannel1.Checked;
            _audioManager.Generator.EnableChannel2 = chkChannel2.Checked;
            _audioManager.Play();
            btnPlayAudio.Text = "■ Stop";
        }
    }

    private void NumFrequency_ValueChanged(object? sender, EventArgs e)
    {
        _audioManager.Generator.Frequency = (float)numFrequency.Value;
    }

    private void TrkAmplitude_Scroll(object? sender, EventArgs e)
    {
        _audioManager.Generator.Amplitude = trkAmplitude.Value / 100f;
        lblAmplitude.Text = $"{trkAmplitude.Value}%";
    }

    private void ChkChannel_CheckedChanged(object? sender, EventArgs e)
    {
        _audioManager.Generator.EnableChannel1 = chkChannel1.Checked;
        _audioManager.Generator.EnableChannel2 = chkChannel2.Checked;
    }

    private void ChkEnableFilter_CheckedChanged(object? sender, EventArgs e)
    {
        _signalSeparator.IsEnabled = chkEnableFilter.Checked;
    }

    private void NumCutoff_ValueChanged(object? sender, EventArgs e)
    {
        // Note: Changing cutoff requires recreating filter
        // For simplicity, we'll just show the value here
        lblCutoffValue.Text = $"{numCutoff.Value:F1} Hz";
    }

    private void CmbSignalType_SelectedIndexChanged(object? sender, EventArgs e)
    {
        _audioManager.Generator.SignalType = cmbSignalType.SelectedIndex switch
        {
            0 => SignalType.Sine,
            1 => SignalType.BandLimitedNoise,
            2 => SignalType.SnowTexture,
            _ => SignalType.Sine
        };
    }

    private void ChkVibrationComp_CheckedChanged(object? sender, EventArgs e)
    {
        _vibrationCompensator.IsEnabled = chkVibrationComp.Checked;
        UpdateCompStatus();
    }

    private void ChkUseNotch_CheckedChanged(object? sender, EventArgs e)
    {
        _vibrationCompensator.UseNotchFallback = chkUseNotch.Checked;
        UpdateCompStatus();
    }

    private void BtnResetComp_Click(object? sender, EventArgs e)
    {
        _vibrationCompensator.Reset();
        UpdateCompStatus();
    }

    private void UpdateCompStatus()
    {
        if (!_vibrationCompensator.IsEnabled)
        {
            lblCompStatus.Text = "";
            return;
        }

        string mode = _vibrationCompensator.UseNotchFallback ? "Notch" : "NLMS";
        string conv = _vibrationCompensator.IsConverged ? " ✓" : "";
        float snr = _vibrationCompensator.SnrImprovement;
        float h1x = _vibrationCompensator.GetHarmonicAmplitude(0, 1);
        float h1y = _vibrationCompensator.GetHarmonicAmplitude(1, 1);
        lblCompStatus.Text = $"{mode}{conv} SNR:{snr:F1}dB H1:({h1x:F1},{h1y:F1})";
    }

    private void ChkStream_CheckedChanged(object? sender, EventArgs e)
    {
        _streamer.IsEnabled = chkStream.Checked;
        if (chkStream.Checked)
        {
            _streamer.UpdateEndpoint();
            _commandReceiver.Start();
            _commandPollTimer.Start();
            lblStatus.Text = $"Streaming UDP:{_streamer.TargetPort} | Listening:{_commandReceiver.ListenPort}";
        }
        else
        {
            _commandPollTimer.Stop();
            _commandReceiver.Stop();
        }
    }
    
    private void OnCommandPoll(object? sender, EventArgs e)
    {
        // Process incoming commands
        while (_commandReceiver.TryReceive(out string cmd))
        {
            _hapticController.ProcessCommand(cmd);
            
            // Handle CAL_START specially
            if (cmd.StartsWith("CAL_START", StringComparison.OrdinalIgnoreCase))
            {
                if (!_copCalculator.IsCalibrating)
                    _copCalculator.StartCalibration();
            }
        }
        
        // Safety checks
        _hapticController.CheckSafety();
        
        // Update UI with client connection status
        if (_hapticController.IsClientConnected)
        {
            var vel = _hapticController.CurrentVelocity;
            lblCompStatus.Text = $"Quest ● Vel:{vel:F2}";
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _commandPollTimer.Stop();
        _audioManager.Dispose();
        _boardReader.Dispose();
        _streamer.Dispose();
        _commandReceiver.Dispose();
        base.OnFormClosing(e);
    }
}

