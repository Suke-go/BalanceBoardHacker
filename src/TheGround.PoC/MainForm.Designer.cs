namespace TheGround.PoC;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
        this.Text = "TheGround PoC - Balance Board Haptic Feedback";
        this.Size = new System.Drawing.Size(1200, 750);
        this.MinimumSize = new System.Drawing.Size(1000, 600);
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Font = new System.Drawing.Font("Segoe UI", 9F);
        this.BackColor = System.Drawing.Color.FromArgb(240, 240, 245);
        this.Padding = new System.Windows.Forms.Padding(10);

        // Main split: Left (CoP) | Right (Controls)
        var mainTable = new System.Windows.Forms.TableLayoutPanel
        {
            Dock = System.Windows.Forms.DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new System.Windows.Forms.Padding(5)
        };
        mainTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 45F));
        mainTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 55F));
        this.Controls.Add(mainTable);

        // === LEFT PANEL: CoP Visualization ===
        var leftPanel = new System.Windows.Forms.TableLayoutPanel
        {
            Dock = System.Windows.Forms.DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Margin = new System.Windows.Forms.Padding(5)
        };
        leftPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 90F));
        leftPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 10F));
        mainTable.Controls.Add(leftPanel, 0, 0);

        panelCoP = new System.Windows.Forms.Panel
        {
            Dock = System.Windows.Forms.DockStyle.Fill,
            BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
            BackColor = System.Drawing.Color.White,
            Margin = new System.Windows.Forms.Padding(5)
        };
        panelCoP.Paint += PanelCoP_Paint;
        leftPanel.Controls.Add(panelCoP, 0, 0);

        // Legend
        var legendPanel = new System.Windows.Forms.FlowLayoutPanel
        {
            Dock = System.Windows.Forms.DockStyle.Fill,
            BackColor = System.Drawing.Color.White,
            Padding = new System.Windows.Forms.Padding(10, 5, 10, 5),
            Margin = new System.Windows.Forms.Padding(5)
        };
        leftPanel.Controls.Add(legendPanel, 0, 1);

        var pnlRed = new System.Windows.Forms.Panel { BackColor = System.Drawing.Color.Red, Size = new System.Drawing.Size(16, 16), Margin = new System.Windows.Forms.Padding(5, 3, 3, 3) };
        var lblRed = new System.Windows.Forms.Label { Text = "Raw CoP", AutoSize = true, Margin = new System.Windows.Forms.Padding(0, 3, 20, 3) };
        var pnlGreen = new System.Windows.Forms.Panel { BackColor = System.Drawing.Color.Green, Size = new System.Drawing.Size(16, 16), Margin = new System.Windows.Forms.Padding(5, 3, 3, 3) };
        var lblGreen = new System.Windows.Forms.Label { Text = "Filtered CoP", AutoSize = true, Margin = new System.Windows.Forms.Padding(0, 3, 3, 3) };
        legendPanel.Controls.AddRange(new System.Windows.Forms.Control[] { pnlRed, lblRed, pnlGreen, lblGreen });

        // === RIGHT PANEL: Controls ===
        var rightPanel = new System.Windows.Forms.TableLayoutPanel
        {
            Dock = System.Windows.Forms.DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1,
            Margin = new System.Windows.Forms.Padding(5)
        };
        rightPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        rightPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        rightPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        rightPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        mainTable.Controls.Add(rightPanel, 1, 0);

        // === Group 1: Connection ===
        var grpConnection = new System.Windows.Forms.GroupBox
        {
            Text = "Balance Board",
            Dock = System.Windows.Forms.DockStyle.Fill,
            AutoSize = true,
            Padding = new System.Windows.Forms.Padding(10),
            Margin = new System.Windows.Forms.Padding(5),
            BackColor = System.Drawing.Color.White
        };
        rightPanel.Controls.Add(grpConnection, 0, 0);

        var connFlow = new System.Windows.Forms.FlowLayoutPanel { Dock = System.Windows.Forms.DockStyle.Fill, AutoSize = true, WrapContents = false };
        grpConnection.Controls.Add(connFlow);

        btnConnect = new System.Windows.Forms.Button
        {
            Text = "Connect",
            Size = new System.Drawing.Size(120, 35),
            Margin = new System.Windows.Forms.Padding(5),
            FlatStyle = System.Windows.Forms.FlatStyle.Flat,
            BackColor = System.Drawing.Color.SteelBlue,
            ForeColor = System.Drawing.Color.White
        };
        btnConnect.Click += BtnConnect_Click;
        connFlow.Controls.Add(btnConnect);

        btnCalibrate = new System.Windows.Forms.Button
        {
            Text = "Calibrate",
            Size = new System.Drawing.Size(120, 35),
            Margin = new System.Windows.Forms.Padding(5),
            FlatStyle = System.Windows.Forms.FlatStyle.Flat,
            BackColor = System.Drawing.Color.SeaGreen,
            ForeColor = System.Drawing.Color.White
        };
        btnCalibrate.Click += BtnCalibrate_Click;
        connFlow.Controls.Add(btnCalibrate);

        btnBluetoothSetup = new System.Windows.Forms.Button
        {
            Text = "BT Setup",
            Size = new System.Drawing.Size(100, 35),
            Margin = new System.Windows.Forms.Padding(5),
            FlatStyle = System.Windows.Forms.FlatStyle.Flat,
            BackColor = System.Drawing.Color.Purple,
            ForeColor = System.Drawing.Color.White
        };
        btnBluetoothSetup.Click += BtnBluetoothSetup_Click;
        connFlow.Controls.Add(btnBluetoothSetup);

        chkStream = new System.Windows.Forms.CheckBox
        {
            Text = "Stream UDP",
            AutoSize = true,
            Margin = new System.Windows.Forms.Padding(15, 10, 5, 5),
            Font = new System.Drawing.Font("Segoe UI", 10F)
        };
        chkStream.CheckedChanged += ChkStream_CheckedChanged;
        connFlow.Controls.Add(chkStream);

        lblStatus = new System.Windows.Forms.Label
        {
            Text = "Disconnected - Click 'BT Setup' first",
            AutoSize = true,
            Margin = new System.Windows.Forms.Padding(10, 12, 5, 5),
            ForeColor = System.Drawing.Color.DarkBlue,
            Font = new System.Drawing.Font("Segoe UI", 10F)
        };
        connFlow.Controls.Add(lblStatus);

        // === Group 2: Sensors ===
        var grpSensors = new System.Windows.Forms.GroupBox
        {
            Text = "Sensor Values",
            Dock = System.Windows.Forms.DockStyle.Fill,
            AutoSize = true,
            Padding = new System.Windows.Forms.Padding(10),
            Margin = new System.Windows.Forms.Padding(5),
            BackColor = System.Drawing.Color.White
        };
        rightPanel.Controls.Add(grpSensors, 0, 1);

        var sensorTable = new System.Windows.Forms.TableLayoutPanel { Dock = System.Windows.Forms.DockStyle.Fill, AutoSize = true, ColumnCount = 5, RowCount = 2 };
        grpSensors.Controls.Add(sensorTable);

        lblTL = new System.Windows.Forms.Label { Text = "TL: -- kg", AutoSize = true, Font = new System.Drawing.Font("Consolas", 10F), Margin = new System.Windows.Forms.Padding(5) };
        lblTR = new System.Windows.Forms.Label { Text = "TR: -- kg", AutoSize = true, Font = new System.Drawing.Font("Consolas", 10F), Margin = new System.Windows.Forms.Padding(5) };
        lblBL = new System.Windows.Forms.Label { Text = "BL: -- kg", AutoSize = true, Font = new System.Drawing.Font("Consolas", 10F), Margin = new System.Windows.Forms.Padding(5) };
        lblBR = new System.Windows.Forms.Label { Text = "BR: -- kg", AutoSize = true, Font = new System.Drawing.Font("Consolas", 10F), Margin = new System.Windows.Forms.Padding(5) };
        lblTotal = new System.Windows.Forms.Label { Text = "Total: -- kg", AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold), ForeColor = System.Drawing.Color.DarkSlateBlue, Margin = new System.Windows.Forms.Padding(15, 5, 5, 5) };
        lblRawCoP = new System.Windows.Forms.Label { Text = "Raw: (--, --)", AutoSize = true, Font = new System.Drawing.Font("Consolas", 10F), ForeColor = System.Drawing.Color.Red, Margin = new System.Windows.Forms.Padding(5) };
        lblFilteredCoP = new System.Windows.Forms.Label { Text = "Filtered: (--, --)", AutoSize = true, Font = new System.Drawing.Font("Consolas", 10F), ForeColor = System.Drawing.Color.Green, Margin = new System.Windows.Forms.Padding(5) };

        sensorTable.Controls.Add(lblTL, 0, 0);
        sensorTable.Controls.Add(lblTR, 1, 0);
        sensorTable.Controls.Add(lblTotal, 2, 0);
        sensorTable.SetRowSpan(lblTotal, 2);
        sensorTable.Controls.Add(lblRawCoP, 3, 0);
        sensorTable.Controls.Add(lblFilteredCoP, 4, 0);
        sensorTable.Controls.Add(lblBL, 0, 1);
        sensorTable.Controls.Add(lblBR, 1, 1);

        // === Group 3: Filter ===
        var grpFilter = new System.Windows.Forms.GroupBox
        {
            Text = "Signal Separation (Low-Pass Filter)",
            Dock = System.Windows.Forms.DockStyle.Fill,
            AutoSize = true,
            Padding = new System.Windows.Forms.Padding(10),
            Margin = new System.Windows.Forms.Padding(5),
            BackColor = System.Drawing.Color.White
        };
        rightPanel.Controls.Add(grpFilter, 0, 2);

        var filterFlow = new System.Windows.Forms.FlowLayoutPanel { Dock = System.Windows.Forms.DockStyle.Fill, AutoSize = true, WrapContents = false };
        grpFilter.Controls.Add(filterFlow);

        chkEnableFilter = new System.Windows.Forms.CheckBox { Text = "Enable", AutoSize = true, Checked = true, Margin = new System.Windows.Forms.Padding(5, 8, 10, 5) };
        chkEnableFilter.CheckedChanged += ChkEnableFilter_CheckedChanged;
        filterFlow.Controls.Add(chkEnableFilter);

        filterFlow.Controls.Add(new System.Windows.Forms.Label { Text = "Cutoff:", AutoSize = true, Margin = new System.Windows.Forms.Padding(10, 10, 3, 5) });

        numCutoff = new System.Windows.Forms.NumericUpDown { Size = new System.Drawing.Size(60, 25), Minimum = 1, Maximum = 20, Value = 6, DecimalPlaces = 1, Margin = new System.Windows.Forms.Padding(3, 5, 3, 5) };
        numCutoff.ValueChanged += NumCutoff_ValueChanged;
        filterFlow.Controls.Add(numCutoff);

        lblCutoffValue = new System.Windows.Forms.Label { Text = "Hz", AutoSize = true, Margin = new System.Windows.Forms.Padding(0, 10, 20, 5) };
        filterFlow.Controls.Add(lblCutoffValue);

        chkShowRaw = new System.Windows.Forms.CheckBox { Text = "Show Raw", AutoSize = true, Checked = true, ForeColor = System.Drawing.Color.Red, Margin = new System.Windows.Forms.Padding(10, 8, 10, 5) };
        filterFlow.Controls.Add(chkShowRaw);

        chkShowFiltered = new System.Windows.Forms.CheckBox { Text = "Show Filtered", AutoSize = true, Checked = true, ForeColor = System.Drawing.Color.Green, Margin = new System.Windows.Forms.Padding(10, 8, 5, 5) };
        filterFlow.Controls.Add(chkShowFiltered);

        // === Group 4: Audio ===
        var grpAudio = new System.Windows.Forms.GroupBox
        {
            Text = "Audio Output (Bass Shaker)",
            Dock = System.Windows.Forms.DockStyle.Fill,
            AutoSize = true,
            Padding = new System.Windows.Forms.Padding(10),
            Margin = new System.Windows.Forms.Padding(5),
            BackColor = System.Drawing.Color.White
        };
        rightPanel.Controls.Add(grpAudio, 0, 3);

        var audioTable = new System.Windows.Forms.TableLayoutPanel { Dock = System.Windows.Forms.DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = 4 };
        grpAudio.Controls.Add(audioTable);

        // Row 1: Device
        var deviceFlow = new System.Windows.Forms.FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new System.Windows.Forms.Padding(0, 0, 0, 5) };
        audioTable.Controls.Add(deviceFlow, 0, 0);
        deviceFlow.Controls.Add(new System.Windows.Forms.Label { Text = "Output Device:", AutoSize = true, Margin = new System.Windows.Forms.Padding(5, 8, 5, 5) });
        cmbAudioDevice = new System.Windows.Forms.ComboBox { Size = new System.Drawing.Size(400, 25), DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList, Margin = new System.Windows.Forms.Padding(5) };
        deviceFlow.Controls.Add(cmbAudioDevice);

        // Row 2: Freq + Amp
        var freqAmpFlow = new System.Windows.Forms.FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new System.Windows.Forms.Padding(0, 0, 0, 5) };
        audioTable.Controls.Add(freqAmpFlow, 0, 1);

        freqAmpFlow.Controls.Add(new System.Windows.Forms.Label { Text = "Frequency:", AutoSize = true, Margin = new System.Windows.Forms.Padding(5, 8, 3, 5) });
        numFrequency = new System.Windows.Forms.NumericUpDown { Size = new System.Drawing.Size(65, 25), Minimum = 10, Maximum = 80, Value = 30, Margin = new System.Windows.Forms.Padding(3, 5, 3, 5) };
        numFrequency.ValueChanged += NumFrequency_ValueChanged;
        freqAmpFlow.Controls.Add(numFrequency);
        freqAmpFlow.Controls.Add(new System.Windows.Forms.Label { Text = "Hz", AutoSize = true, Margin = new System.Windows.Forms.Padding(0, 8, 20, 5) });

        freqAmpFlow.Controls.Add(new System.Windows.Forms.Label { Text = "Amplitude:", AutoSize = true, Margin = new System.Windows.Forms.Padding(10, 8, 3, 5) });
        trkAmplitude = new System.Windows.Forms.TrackBar { Size = new System.Drawing.Size(150, 35), Minimum = 0, Maximum = 100, Value = 50, TickFrequency = 25, Margin = new System.Windows.Forms.Padding(3, 0, 3, 0) };
        trkAmplitude.Scroll += TrkAmplitude_Scroll;
        freqAmpFlow.Controls.Add(trkAmplitude);
        lblAmplitude = new System.Windows.Forms.Label { Text = "50%", AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold), Margin = new System.Windows.Forms.Padding(3, 8, 5, 5) };
        freqAmpFlow.Controls.Add(lblAmplitude);

        // Row 3: Signal Type + Channels
        var channelFlow = new System.Windows.Forms.FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new System.Windows.Forms.Padding(0, 0, 0, 5) };
        audioTable.Controls.Add(channelFlow, 0, 2);

        channelFlow.Controls.Add(new System.Windows.Forms.Label { Text = "Signal:", AutoSize = true, Margin = new System.Windows.Forms.Padding(5, 8, 3, 5) });
        cmbSignalType = new System.Windows.Forms.ComboBox 
        { 
            Size = new System.Drawing.Size(100, 25), 
            DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList,
            Margin = new System.Windows.Forms.Padding(3, 5, 15, 5) 
        };
        cmbSignalType.Items.AddRange(new object[] { "Sine", "Noise", "Snow" });
        cmbSignalType.SelectedIndex = 0;
        cmbSignalType.SelectedIndexChanged += CmbSignalType_SelectedIndexChanged;
        channelFlow.Controls.Add(cmbSignalType);

        chkChannel1 = new System.Windows.Forms.CheckBox { Text = "CH1 (L)", AutoSize = true, Checked = true, Margin = new System.Windows.Forms.Padding(5, 5, 10, 5) };
        chkChannel1.CheckedChanged += ChkChannel_CheckedChanged;
        channelFlow.Controls.Add(chkChannel1);

        chkChannel2 = new System.Windows.Forms.CheckBox { Text = "CH2 (R)", AutoSize = true, Checked = true, Margin = new System.Windows.Forms.Padding(5) };
        chkChannel2.CheckedChanged += ChkChannel_CheckedChanged;
        channelFlow.Controls.Add(chkChannel2);

        // Row 4: Compensation
        var compFlow = new System.Windows.Forms.FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new System.Windows.Forms.Padding(0, 0, 0, 5) };
        audioTable.Controls.Add(compFlow, 0, 3);

        chkVibrationComp = new System.Windows.Forms.CheckBox { Text = "Vib. Compensation", AutoSize = true, Margin = new System.Windows.Forms.Padding(5) };
        chkVibrationComp.CheckedChanged += ChkVibrationComp_CheckedChanged;
        compFlow.Controls.Add(chkVibrationComp);

        chkUseNotch = new System.Windows.Forms.CheckBox { Text = "Notch Filter", AutoSize = true, Margin = new System.Windows.Forms.Padding(10, 5, 5, 5) };
        chkUseNotch.CheckedChanged += ChkUseNotch_CheckedChanged;
        compFlow.Controls.Add(chkUseNotch);

        lblCompStatus = new System.Windows.Forms.Label { Text = "", AutoSize = true, Margin = new System.Windows.Forms.Padding(15, 8, 5, 5), ForeColor = System.Drawing.Color.DarkGreen };
        compFlow.Controls.Add(lblCompStatus);

        // Row 5: Play
        var playFlow = new System.Windows.Forms.FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new System.Windows.Forms.Padding(0) };
        audioTable.Controls.Add(playFlow, 0, 4);

        btnPlayAudio = new System.Windows.Forms.Button
        {
            Text = "▶ Play",
            Size = new System.Drawing.Size(120, 38),
            Margin = new System.Windows.Forms.Padding(5, 5, 10, 5),
            Font = new System.Drawing.Font("Segoe UI", 11F),
            FlatStyle = System.Windows.Forms.FlatStyle.Flat,
            BackColor = System.Drawing.Color.DarkOrange,
            ForeColor = System.Drawing.Color.White
        };
        btnPlayAudio.Click += BtnPlayAudio_Click;
        playFlow.Controls.Add(btnPlayAudio);

        btnResetComp = new System.Windows.Forms.Button
        {
            Text = "Reset Comp",
            Size = new System.Drawing.Size(100, 38),
            Margin = new System.Windows.Forms.Padding(5),
            Font = new System.Drawing.Font("Segoe UI", 9F),
            FlatStyle = System.Windows.Forms.FlatStyle.Flat,
            BackColor = System.Drawing.Color.Gray,
            ForeColor = System.Drawing.Color.White
        };
        btnResetComp.Click += BtnResetComp_Click;
        playFlow.Controls.Add(btnResetComp);
        
        // Demo Mode selection
        playFlow.Controls.Add(new System.Windows.Forms.Label 
        { 
            Text = "Demo:", 
            AutoSize = true, 
            Margin = new System.Windows.Forms.Padding(20, 12, 5, 5),
            Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold)
        });
        
        rbDemoOff = new System.Windows.Forms.RadioButton 
        { 
            Text = "Off", 
            AutoSize = true, 
            Checked = true, 
            Margin = new System.Windows.Forms.Padding(5, 8, 5, 5) 
        };
        rbDemoOff.CheckedChanged += RbDemo_CheckedChanged;
        playFlow.Controls.Add(rbDemoOff);
        
        rbDemoSkiJump = new System.Windows.Forms.RadioButton 
        { 
            Text = "🎿 Ski Jump", 
            AutoSize = true, 
            Margin = new System.Windows.Forms.Padding(5, 8, 5, 5),
            ForeColor = System.Drawing.Color.DarkBlue
        };
        rbDemoSkiJump.CheckedChanged += RbDemo_CheckedChanged;
        playFlow.Controls.Add(rbDemoSkiJump);
        
        rbDemoTilt = new System.Windows.Forms.RadioButton 
        { 
            Text = "↔️ L/R Tilt", 
            AutoSize = true, 
            Margin = new System.Windows.Forms.Padding(5, 8, 5, 5),
            ForeColor = System.Drawing.Color.DarkGreen
        };
        rbDemoTilt.CheckedChanged += RbDemo_CheckedChanged;
        playFlow.Controls.Add(rbDemoTilt);
        
        rbDemoUnified = new System.Windows.Forms.RadioButton 
        { 
            Text = "⛷️ Unified", 
            AutoSize = true, 
            Margin = new System.Windows.Forms.Padding(5, 8, 5, 5),
            ForeColor = System.Drawing.Color.DarkRed,
            Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold)
        };
        rbDemoUnified.CheckedChanged += RbDemo_CheckedChanged;
        playFlow.Controls.Add(rbDemoUnified);
        
        lblDemoStatus = new System.Windows.Forms.Label 
        { 
            Text = "", 
            AutoSize = true, 
            Margin = new System.Windows.Forms.Padding(10, 12, 5, 5),
            ForeColor = System.Drawing.Color.Purple,
            Font = new System.Drawing.Font("Consolas", 9F)
        };
        playFlow.Controls.Add(lblDemoStatus);
    }

    #endregion

    // Controls
    private System.Windows.Forms.Panel panelCoP;
    private System.Windows.Forms.Button btnConnect;
    private System.Windows.Forms.Button btnCalibrate;
    private System.Windows.Forms.Button btnBluetoothSetup;
    private System.Windows.Forms.Label lblStatus;
    private System.Windows.Forms.Label lblTL, lblTR, lblBL, lblBR, lblTotal;
    private System.Windows.Forms.Label lblRawCoP, lblFilteredCoP;
    private System.Windows.Forms.CheckBox chkEnableFilter;
    private System.Windows.Forms.NumericUpDown numCutoff;
    private System.Windows.Forms.Label lblCutoffValue;
    private System.Windows.Forms.CheckBox chkShowRaw, chkShowFiltered;
    private System.Windows.Forms.ComboBox cmbAudioDevice;
    private System.Windows.Forms.ComboBox cmbSignalType;
    private System.Windows.Forms.NumericUpDown numFrequency;
    private System.Windows.Forms.TrackBar trkAmplitude;
    private System.Windows.Forms.Label lblAmplitude;
    private System.Windows.Forms.CheckBox chkChannel1, chkChannel2;
    private System.Windows.Forms.CheckBox chkVibrationComp, chkUseNotch;
    private System.Windows.Forms.Label lblCompStatus;
    private System.Windows.Forms.Button btnPlayAudio;
    private System.Windows.Forms.Button btnResetComp;
    private System.Windows.Forms.CheckBox chkStream;
    private System.Windows.Forms.RadioButton rbDemoOff;
    private System.Windows.Forms.RadioButton rbDemoSkiJump;
    private System.Windows.Forms.RadioButton rbDemoTilt;
    private System.Windows.Forms.RadioButton rbDemoUnified;
    private System.Windows.Forms.Label lblDemoStatus;
}

