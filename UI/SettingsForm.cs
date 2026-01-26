using DepthClockWallpaper.Core;
using DepthClockWallpaper.Models;
using Microsoft.Extensions.Options;
using Microsoft.Win32;
using System.Diagnostics;

namespace DepthClockWallpaper.UI;

public partial class SettingsForm : Form
{
    private readonly IOptionsMonitor<AppConfig> _config;
    private readonly IWritableOptions<AppConfig> _writableConfig;
    private readonly Orchestrator _orchestrator;
    private NotifyIcon _trayIcon;
    private System.Windows.Forms.Timer _bingUpdateTimer;

    private ComboBox _modeComboBox;
    private ComboBox _imageComboBox;
    private TextBox _timeFormatTextBox;
    private NumericUpDown _updateIntervalBox;
    private CheckBox _launchOnStartupCheckBox;
    private TrackBar _verticalSlider;
    private TrackBar _horizontalSlider;
    private Button _applyButton;
    private Button _openTempFolderButton;
    private Label _verticalLabel;
    private Label _horizontalLabel;
    private Panel _customImagePanel;
    private Button _browseButton;
    private Label _lastBingUpdateLabel;

    // New settings controls
    private CheckBox _cacheDepthMaskCheckBox;
    private ComboBox _thresholdComboBox;
    private NumericUpDown _thresholdPercentileBox;
    private NumericUpDown _maskBlurBox;
    private ComboBox _fontFamilyComboBox;
    private ComboBox _fontStyleComboBox;
    private Button _clockColorButton;
    private Button _shadowColorButton;
    private NumericUpDown _shadowOpacityBox;
    private NumericUpDown _shadowBlurBox;
    private NumericUpDown _shadowOffsetXBox;
    private NumericUpDown _shadowOffsetYBox;
    private NumericUpDown _fontSizeBox;

    private CheckBox _autoPositionCheckBox;
    private TrackBar _maxCoverageSlider;
    private Label _maxCoverageLabel;
    private Label _maxCoverageValueLabel;
    private ComboBox _positionStrategyComboBox;
    private Label _positionStrategyLabel;
    private Label _manualPositionLabel;

    // Debug settings controls
    private CheckBox _enableDebugModeCheckBox;
    private TextBox _debugPathTextBox;
    private Button _viewCrashLogsButton;

    // Flag to prevent heavy operations during initialization
    private bool _isInitializing = true;

    public SettingsForm(Orchestrator orchestrator, IOptionsMonitor<AppConfig> config, IWritableOptions<AppConfig> writableConfig)
    {
        _orchestrator = orchestrator;
        _config = config;
        _writableConfig = writableConfig;

        try
        {
            InitializeComponent();
            InitializeTrayIcon();
            LoadSettingsToUI();

            // Hide the main settings window initially
            WindowState = FormWindowState.Minimized;
            ShowInTaskbar = false;
            Visible = false;

            // Defer heavy initialization to Load event to prevent UI freeze
            Load += OnFormLoad;

            Task.Run(() =>
            {
                _orchestrator.UpdateWallpaper();
                _orchestrator.Start();
            });
        }
        catch (Exception ex)
        {
            CrashLogger.Log(ex);
            MessageBox.Show($"Failed to start application. Crash report saved to crash.log.\n\n{ex.Message}",
                "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.Exit(1);
        }
    }

    private void InitializeComponent()
    {
        // Form setup
        Text = "DepthClockWallpaper Settings";
        Size = new Size(700, 850);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = LoadApplicationIcon();
        BackColor = Color.FromArgb(245, 245, 245);
        Font = new Font("Segoe UI", 9F);

        // Create scrollable main panel
        var scrollPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(20)
        };

        var mainPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Padding = new Padding(0, 0, 20, 0)
        };

        // === WALLPAPER SOURCE SECTION ===
        var sourceGroup = CreateGroupBox("Wallpaper Source");
        var sourceLayout = CreateFormLayout();

        sourceLayout.Controls.Add(CreateLabel("Mode:"), 0, 0);
        _modeComboBox = CreateComboBox(new[] { "Custom Image", "Bing Wallpaper" });
        _modeComboBox.SelectedIndexChanged += ModeChanged;
        sourceLayout.Controls.Add(_modeComboBox, 1, 0);

        sourceLayout.Controls.Add(CreateLabel("Image:"), 0, 1);
        _customImagePanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Margin = new Padding(0)
        };
        _imageComboBox = CreateComboBox(new string[] { });
        _imageComboBox.Width = 300;
        _browseButton = CreateButton("Browse...", 90);
        _browseButton.Click += BrowseImage;
        _customImagePanel.Controls.Add(_imageComboBox);
        _customImagePanel.Controls.Add(_browseButton);
        sourceLayout.Controls.Add(_customImagePanel, 1, 1);

        sourceLayout.Controls.Add(CreateLabel("Update Interval:"), 0, 2);
        var intervalPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Margin = new Padding(0)
        };
        _updateIntervalBox = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 1440,
            Value = decimal.Round((_config.CurrentValue.Performance.UpdateInterval / 60000), 2),
            Width = 80,
            Font = new Font("Segoe UI", 9F)
        };
        intervalPanel.Controls.Add(_updateIntervalBox);
        intervalPanel.Controls.Add(new Label { Text = " minutes", AutoSize = true, Padding = new Padding(5, 4, 0, 0) });
        sourceLayout.Controls.Add(intervalPanel, 1, 2);

        sourceLayout.Controls.Add(CreateLabel("Bing Status:"), 0, 3);
        _lastBingUpdateLabel = new Label
        {
            Text = "Not checked yet",
            AutoSize = true,
            ForeColor = Color.Gray,
            Padding = new Padding(0, 4, 0, 0)
        };
        sourceLayout.Controls.Add(_lastBingUpdateLabel, 1, 3);

        sourceGroup.Controls.Add(sourceLayout);
        mainPanel.Controls.Add(sourceGroup);

        // === CLOCK POSITION SECTION ===
        var positionGroup = CreateGroupBox("Clock Position");
        var positionLayout = CreateFormLayout();

        positionLayout.Controls.Add(CreateLabel("Time Format:"), 0, 0);
        _timeFormatTextBox = new TextBox
        {
            Width = 400,
            Font = new Font("Segoe UI", 9F),
            Text = _config.CurrentValue.Clock.Format
        };
        positionLayout.Controls.Add(_timeFormatTextBox, 1, 0);

        _autoPositionCheckBox = new CheckBox
        {
            Text = "Auto Position Mode",
            AutoSize = true,
            Padding = new Padding(0, 6, 0, 0),
            Checked = _config.CurrentValue.Clock.Position.AutoEnabled
        };
        _autoPositionCheckBox.CheckedChanged += (s, e) => UpdatePositionControlsEnabled();
        positionLayout.Controls.Add(_autoPositionCheckBox, 0, 1);
        positionLayout.Controls.Add(new Label(), 1, 1);

        _positionStrategyLabel = CreateLabel("Strategy:");
        positionLayout.Controls.Add(_positionStrategyLabel, 0, 2);
        _positionStrategyComboBox = CreateComboBox(new[] { "Lowest Coverage", "Edges First", "Smart Hybrid" });
        positionLayout.Controls.Add(_positionStrategyComboBox, 1, 2);

        _maxCoverageLabel = CreateLabel("Max Coverage:");
        positionLayout.Controls.Add(_maxCoverageLabel, 0, 3);
        var coveragePanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Margin = new Padding(0)
        };
        _maxCoverageSlider = new TrackBar
        {
            Minimum = 0,
            Maximum = 100,
            TickFrequency = 10,
            Width = 250,
            Value = (int)(_config.CurrentValue.Clock.Position.MaxCoveragePercent * 100)
        };
        _maxCoverageValueLabel = new Label
        {
            Text = $"{_maxCoverageSlider.Value}%",
            AutoSize = true,
            Padding = new Padding(5, 6, 0, 0),
            Font = new Font("Segoe UI", 9F)
        };
        _maxCoverageSlider.ValueChanged += (s, e) => _maxCoverageValueLabel.Text = $"{_maxCoverageSlider.Value}%";
        coveragePanel.Controls.Add(_maxCoverageSlider);
        coveragePanel.Controls.Add(_maxCoverageValueLabel);
        positionLayout.Controls.Add(coveragePanel, 1, 3);

        _manualPositionLabel = CreateLabel("Manual Position (disabled in auto mode)");
        _manualPositionLabel.ForeColor = Color.Gray;
        _manualPositionLabel.Padding = new Padding(0, 15, 0, 0);
        positionLayout.Controls.Add(_manualPositionLabel, 0, 4);
        positionLayout.Controls.Add(new Label(), 1, 4);

        _horizontalLabel = CreateLabel("Horizontal: 50%");
        positionLayout.Controls.Add(_horizontalLabel, 0, 5);
        _horizontalSlider = new TrackBar
        {
            Minimum = 0,
            Maximum = 100,
            TickFrequency = 10,
            Width = 400,
            Value = (int)(_config.CurrentValue.Clock.Position.Horizontal * 100)
        };
        _horizontalSlider.ValueChanged += (s, e) => _horizontalLabel.Text = $"Horizontal: {_horizontalSlider.Value}%";
        positionLayout.Controls.Add(_horizontalSlider, 1, 5);

        _verticalLabel = CreateLabel("Vertical: 50%");
        positionLayout.Controls.Add(_verticalLabel, 0, 6);
        _verticalSlider = new TrackBar
        {
            Minimum = 0,
            Maximum = 100,
            TickFrequency = 10,
            Width = 400,
            Value = (int)(_config.CurrentValue.Clock.Position.Vertical * 100)
        };
        _verticalSlider.ValueChanged += (s, e) => _verticalLabel.Text = $"Vertical: {_verticalSlider.Value}%";
        positionLayout.Controls.Add(_verticalSlider, 1, 6);

        positionGroup.Controls.Add(positionLayout);
        mainPanel.Controls.Add(positionGroup);

        UpdatePositionControlsEnabled();

        // === CLOCK STYLE SECTION ===
        var styleGroup = CreateGroupBox("Clock Style");
        var styleLayout = CreateFormLayout();

        styleLayout.Controls.Add(CreateLabel("Font Family:"), 0, 0);
        // Start with placeholder - fonts will be loaded asynchronously in OnFormLoad
        _fontFamilyComboBox = CreateComboBox(new[] { "Loading fonts..." });
        styleLayout.Controls.Add(_fontFamilyComboBox, 1, 0);

        styleLayout.Controls.Add(CreateLabel("Font Style:"), 0, 1);
        _fontStyleComboBox = CreateComboBox(new[] { "Regular", "Bold", "Italic", "Bold Italic" });
        styleLayout.Controls.Add(_fontStyleComboBox, 1, 1);

        styleLayout.Controls.Add(CreateLabel("Font Size:"), 0, 2);
        _fontSizeBox = CreateNumericUpDown(1, 200, 9.6m, 2, 1m);
        styleLayout.Controls.Add(_fontSizeBox, 1, 2);

        styleLayout.Controls.Add(CreateLabel("Clock Color:"), 0, 3);
        _clockColorButton = CreateColorButton("#FFFFFF", Color.White, Color.Black);
        _clockColorButton.Click += (s, e) => ShowColorDialog(_clockColorButton);
        styleLayout.Controls.Add(_clockColorButton, 1, 3);

        styleGroup.Controls.Add(styleLayout);
        mainPanel.Controls.Add(styleGroup);

        // === SHADOW SETTINGS SECTION ===
        var shadowGroup = CreateGroupBox("Shadow Settings");
        var shadowLayout = CreateFormLayout();

        shadowLayout.Controls.Add(CreateLabel("Shadow Color:"), 0, 0);
        _shadowColorButton = CreateColorButton("#000000", Color.Black, Color.White);
        _shadowColorButton.Click += (s, e) => ShowColorDialog(_shadowColorButton);
        shadowLayout.Controls.Add(_shadowColorButton, 1, 0);

        shadowLayout.Controls.Add(CreateLabel("Opacity:"), 0, 1);
        _shadowOpacityBox = CreateNumericUpDown(0, 1, 0.60m, 2, 0.1m);
        shadowLayout.Controls.Add(_shadowOpacityBox, 1, 1);

        shadowLayout.Controls.Add(CreateLabel("Blur Radius:"), 0, 2);
        _shadowBlurBox = CreateNumericUpDown(0, 50, 12.0m, 1, 1m);
        shadowLayout.Controls.Add(_shadowBlurBox, 1, 2);

        shadowLayout.Controls.Add(CreateLabel("Offset X:"), 0, 3);
        _shadowOffsetXBox = CreateNumericUpDown(-50, 50, 0.0m, 1, 1m);
        shadowLayout.Controls.Add(_shadowOffsetXBox, 1, 3);

        shadowLayout.Controls.Add(CreateLabel("Offset Y:"), 0, 4);
        _shadowOffsetYBox = CreateNumericUpDown(-50, 50, 6.0m, 1, 1m);
        shadowLayout.Controls.Add(_shadowOffsetYBox, 1, 4);

        shadowGroup.Controls.Add(shadowLayout);
        mainPanel.Controls.Add(shadowGroup);

        // === DEPTH SETTINGS SECTION ===
        var depthGroup = CreateGroupBox("Depth Settings");
        var depthLayout = CreateFormLayout();

        depthLayout.Controls.Add(CreateLabel("Threshold Mode:"), 0, 0);
        _thresholdComboBox = CreateComboBox(new[] { "Auto", "Manual" });
        depthLayout.Controls.Add(_thresholdComboBox, 1, 0);

        depthLayout.Controls.Add(CreateLabel("Threshold Percentile:"), 0, 1);
        _thresholdPercentileBox = CreateNumericUpDown(0, 1, 0.70m, 2, 0.05m);
        depthLayout.Controls.Add(_thresholdPercentileBox, 1, 1);

        depthLayout.Controls.Add(CreateLabel("Mask Blur:"), 0, 2);
        _maskBlurBox = CreateNumericUpDown(0, 50, 8.0m, 1, 1m);
        depthLayout.Controls.Add(_maskBlurBox, 1, 2);

        depthGroup.Controls.Add(depthLayout);
        mainPanel.Controls.Add(depthGroup);

        // === PERFORMANCE SECTION ===
        var perfGroup = CreateGroupBox("Performance");
        var perfLayout = CreateFormLayout();

        perfLayout.Controls.Add(CreateLabel("Cache Depth Mask:"), 0, 0);
        _cacheDepthMaskCheckBox = new CheckBox
        {
            Checked = true,
            AutoSize = true,
            Padding = new Padding(0, 4, 0, 0)
        };
        perfLayout.Controls.Add(_cacheDepthMaskCheckBox, 1, 0);

        perfLayout.Controls.Add(CreateLabel("Enable Debug Mode:"), 0, 1);
        _enableDebugModeCheckBox = new CheckBox
        {
            AutoSize = true,
            Padding = new Padding(0, 4, 0, 0)
        };
        _enableDebugModeCheckBox.CheckedChanged += EnableDebugModeChanged;
        perfLayout.Controls.Add(_enableDebugModeCheckBox, 1, 1);

        perfLayout.Controls.Add(CreateLabel("Debug Path:"), 0, 2);
        _debugPathTextBox = new TextBox
        {
            Width = 350,
            Font = new Font("Segoe UI", 9F)
        };
        perfLayout.Controls.Add(_debugPathTextBox, 1, 2);

        perfGroup.Controls.Add(perfLayout);
        mainPanel.Controls.Add(perfGroup);

        // === SYSTEM SECTION ===
        var systemGroup = CreateGroupBox("System");
        var systemLayout = CreateFormLayout();

        systemLayout.Controls.Add(CreateLabel("Launch on Startup:"), 0, 0);
        _launchOnStartupCheckBox = new CheckBox
        {
            Checked = IsStartupEnabled(),
            AutoSize = true,
            Padding = new Padding(0, 4, 0, 0)
        };
        systemLayout.Controls.Add(_launchOnStartupCheckBox, 1, 0);

        systemLayout.Controls.Add(CreateLabel("View Crash Logs:"), 0, 1);
        _viewCrashLogsButton = new Button
        {
            Text = "Open crash.log",
            Width = 120,
            Height = 26,
            FlatStyle = FlatStyle.System,
            Font = new Font("Segoe UI", 9F),
            Cursor = Cursors.Hand,
            Enabled = CrashLogger.CrashLogExists()
        };
        _viewCrashLogsButton.Click += ViewCrashLogs;
        systemLayout.Controls.Add(_viewCrashLogsButton, 1, 1);

        systemGroup.Controls.Add(systemLayout);
        mainPanel.Controls.Add(systemGroup);

        // === BOTTOM ACTION BUTTONS ===
        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Padding = new Padding(10, 20, 10, 20),
            WrapContents = false
        };

        _applyButton = new Button
        {
            Text = "Apply Settings",
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Width = 140,
            Height = 40,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _applyButton.FlatAppearance.BorderSize = 0;
        _applyButton.Click += async (sender, e) => await ApplySettings();
        buttonPanel.Controls.Add(_applyButton);

        _openTempFolderButton = new Button
        {
            Text = "Open Images Folder",
            BackColor = Color.FromArgb(90, 90, 90),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Width = 160,
            Height = 40,
            Font = new Font("Segoe UI", 9F),
            Cursor = Cursors.Hand,
            Margin = new Padding(10, 0, 0, 0)
        };
        _openTempFolderButton.FlatAppearance.BorderSize = 0;
        _openTempFolderButton.Click += OpenTempFolder;
        buttonPanel.Controls.Add(_openTempFolderButton);

        mainPanel.Controls.Add(buttonPanel);

        scrollPanel.Controls.Add(mainPanel);
        Controls.Add(scrollPanel);

        // Don't call ModeChanged here - defer to Load event to prevent UI freeze
        // ModeChanged(null, null);
        UpdatePositionControlsEnabled();
    }

    private async void OnFormLoad(object? sender, EventArgs e)
    {
        // Now safe to run initialization that may trigger async operations
        _isInitializing = false;

        // Trigger mode-specific initialization (e.g., Bing wallpaper check)
        ModeChanged(null, null);

        // Load system fonts asynchronously to avoid blocking UI
        await LoadFontsAsync();
    }

    // Helper methods for consistent styling
    private GroupBox CreateGroupBox(string title)
    {
        return new GroupBox
        {
            Text = title,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(15),
            Margin = new Padding(0, 0, 0, 15),
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(60, 60, 60),
            Width = 640
        };
    }

    private TableLayoutPanel CreateFormLayout()
    {
        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(5)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        return layout;
    }

    private Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI", 9F),
            ForeColor = Color.FromArgb(80, 80, 80),
            Padding = new Padding(0, 6, 0, 0)
        };
    }

    private ComboBox CreateComboBox(string[] items)
    {
        var combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 200,
            Font = new Font("Segoe UI", 9F)
        };
        combo.Items.AddRange(items);
        return combo;
    }

    private Button CreateButton(string text, int width)
    {
        return new Button
        {
            Text = text,
            Width = width,
            Height = 26,
            FlatStyle = FlatStyle.System,
            Font = new Font("Segoe UI", 9F),
            Cursor = Cursors.Hand
        };
    }

    private Button CreateColorButton(string text, Color backColor, Color foreColor)
    {
        var button = new Button
        {
            Text = text,
            BackColor = backColor,
            ForeColor = foreColor,
            Width = 120,
            Height = 30,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9F),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
        return button;
    }

    private NumericUpDown CreateNumericUpDown(decimal min, decimal max, decimal value, int decimals, decimal increment)
    {
        return new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Value = value,
            DecimalPlaces = decimals,
            Increment = increment,
            Width = 100,
            Font = new Font("Segoe UI", 9F)
        };
    }

    private void InitializeTrayIcon()
    {
        _trayIcon = new NotifyIcon
        {
            Icon = LoadApplicationIcon(),
            Text = "DepthClockWallpaper",
            Visible = true
        };

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.AddRange(new ToolStripItem[]
        {
            new ToolStripMenuItem("Show Settings", null, (s, e) => ShowSettings()),
            new ToolStripMenuItem("Exit", null, (s, e) => ExitApplication())
        });

        _trayIcon.ContextMenuStrip = contextMenu;
        _trayIcon.DoubleClick += (s, e) => ShowSettings();

        // Initialize Bing update timer (check hourly)
        _bingUpdateTimer = new System.Windows.Forms.Timer
        {
            Interval = 3600000 // 1 hour in milliseconds
        };
        _bingUpdateTimer.Tick += CheckForBingUpdates;
        _bingUpdateTimer.Start();
    }

    private void ModeChanged(object? sender, EventArgs? e)
    {
        bool isCustomMode = _modeComboBox.SelectedIndex == 0;
        _customImagePanel.Enabled = isCustomMode;
        _updateIntervalBox.Enabled = !isCustomMode;
        _imageComboBox.Enabled = isCustomMode;
        _browseButton.Enabled = isCustomMode;

        if (isCustomMode)
        {
            LoadCustomImages();
        }
        else
        {
            _lastBingUpdateLabel.Text = "Checking for updates...";
            CheckForBingUpdates(null, null);
        }
    }

    private void EnableDebugModeChanged(object? sender, EventArgs e)
    {
        _debugPathTextBox.Enabled = _enableDebugModeCheckBox.Checked;
        if (!_enableDebugModeCheckBox.Checked)
        {
            _debugPathTextBox.Text = string.Empty;
        }
    }

    private void UpdatePositionControlsEnabled()
    {
        bool autoEnabled = _autoPositionCheckBox?.Checked ?? true;

        if (_positionStrategyComboBox != null)
            _positionStrategyComboBox.Enabled = autoEnabled;

        if (_positionStrategyLabel != null)
            _positionStrategyLabel.Enabled = autoEnabled;

        if (_maxCoverageSlider != null)
            _maxCoverageSlider.Enabled = autoEnabled;

        if (_maxCoverageLabel != null)
            _maxCoverageLabel.Enabled = autoEnabled;

        if (_maxCoverageValueLabel != null)
            _maxCoverageValueLabel.Enabled = autoEnabled;

        if (_horizontalSlider != null)
        {
            _horizontalSlider.Enabled = !autoEnabled;
            _horizontalLabel.Enabled = !autoEnabled;
        }

        if (_verticalSlider != null)
        {
            _verticalSlider.Enabled = !autoEnabled;
            _verticalLabel.Enabled = !autoEnabled;
        }

        if (_manualPositionLabel != null)
            _manualPositionLabel.Visible = !autoEnabled;
    }

    private void LoadCustomImages()
    {
        _imageComboBox.Items.Clear();

        // Add common image locations
        var commonPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "wallpaper.jpg"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "wallpaper.png"),
            "wallpaper.jpg",
            "wallpaper.png"
        };

        foreach (var path in commonPaths)
        {
            if (File.Exists(path))
            {
                _imageComboBox.Items.Add(path);
                if (path == _config.CurrentValue.Wallpaper.Path)
                    _imageComboBox.SelectedItem = path;
            }
        }

        if (_imageComboBox.SelectedIndex == -1 && !string.IsNullOrEmpty(_config.CurrentValue.Wallpaper.Path))
        {
            _imageComboBox.Text = _config.CurrentValue.Wallpaper.Path;
        }
    }

    private void BrowseImage(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp|All Files|*.*",
            Title = "Select Wallpaper Image"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _imageComboBox.Items.Add(dialog.FileName);
            _imageComboBox.SelectedItem = dialog.FileName;
        }
    }

    private async void CheckForBingUpdates(object? sender, EventArgs? e)
    {
        // Skip if still initializing to prevent UI freeze during startup
        if (_isInitializing)
        {
            _lastBingUpdateLabel.Text = "Will check after startup...";
            _lastBingUpdateLabel.ForeColor = Color.Gray;
            return;
        }

        try
        {
            var bingService = new BingWallpaperService();
            var latestImage = await bingService.GetLatestImageAsync().ConfigureAwait(false);

            // Update UI on UI thread
            if (InvokeRequired)
            {
                Invoke(() => UpdateBingStatusLabel(latestImage));
            }
            else
            {
                UpdateBingStatusLabel(latestImage);
            }

            // Check if we're in Bing mode and need to reload - run on background thread!
            if (latestImage != null && _config.CurrentValue.Wallpaper.Mode == EWallpaperMode.Bing)
            {
                Console.WriteLine("Bing image updated, reloading wallpaper on background thread...");
                await Task.Run(() => _orchestrator.UpdateWallpaper()).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            if (InvokeRequired)
            {
                Invoke(() =>
                {
                    _lastBingUpdateLabel.Text = $"Error: {ex.Message}";
                    _lastBingUpdateLabel.ForeColor = Color.Red;
                });
            }
            else
            {
                _lastBingUpdateLabel.Text = $"Error: {ex.Message}";
                _lastBingUpdateLabel.ForeColor = Color.Red;
            }
        }
    }

    private void UpdateBingStatusLabel(BingImage? latestImage)
    {
        if (latestImage != null)
        {
            _lastBingUpdateLabel.Text = $"Updated: {latestImage.Date:yyyy-MM-dd HH:mm}";
            _lastBingUpdateLabel.ForeColor = Color.Green;
        }
        else
        {
            _lastBingUpdateLabel.Text = "No image available";
            _lastBingUpdateLabel.ForeColor = Color.Orange;
        }
    }

    private void CopyCustomImageToTemp(string sourcePath)
    {
        try
        {
            Console.WriteLine($"Copying custom image to temp: {sourcePath}");
            File.Copy(sourcePath, WallpaperPaths.CustomWallpaper, true);
            Console.WriteLine($"✓ Custom image copied to: {WallpaperPaths.CustomWallpaper}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to copy custom image: {ex.Message}");
            throw;
        }
    }

    private void OpenTempFolder(object? sender, EventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = WallpaperPaths.TempDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open folder: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ViewCrashLogs(object? sender, EventArgs e)
    {
        try
        {
            var crashLogPath = CrashLogger.GetCrashLogPath();
            if (File.Exists(crashLogPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = crashLogPath,
                    UseShellExecute = true
                });
            }
            else
            {
                MessageBox.Show("No crash log file found.", "Information",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open crash log: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowColorDialog(Button colorButton)
    {
        var colorDialog = new ColorDialog
        {
            Color = ColorTranslator.FromHtml(colorButton.Text),
            AllowFullOpen = true,
            FullOpen = true
        };

        if (colorDialog.ShowDialog() == DialogResult.OK)
        {
            colorButton.Text = $"#{colorDialog.Color.R:X2}{colorDialog.Color.G:X2}{colorDialog.Color.B:X2}";
            colorButton.BackColor = colorDialog.Color;

            // Adjust text color for readability
            var brightness = (colorDialog.Color.R * 299 + colorDialog.Color.G * 587 + colorDialog.Color.B * 114) / 1000;
            colorButton.ForeColor = brightness > 128 ? Color.Black : Color.White;
        }
    }

    private void LoadSettingsToUI()
    {
        // Mode
        _modeComboBox.SelectedIndex = _config.CurrentValue.Wallpaper.Mode == EWallpaperMode.Custom ? 0 : 1;

        // Time format
        _timeFormatTextBox.Text = _config.CurrentValue.Clock.Format;

        // Position
        _autoPositionCheckBox.Checked = _config.CurrentValue.Clock.Position.AutoEnabled;
        _positionStrategyComboBox.SelectedIndex = (int)_config.CurrentValue.Clock.Position.Strategy;
        _maxCoverageSlider.Value = (int)(_config.CurrentValue.Clock.Position.MaxCoveragePercent * 100);
        _maxCoverageValueLabel.Text = $"{_maxCoverageSlider.Value}%";
        _horizontalSlider.Value = (int)(_config.CurrentValue.Clock.Position.Horizontal * 100);
        _verticalSlider.Value = (int)(_config.CurrentValue.Clock.Position.Vertical * 100);

        // Update interval
        _updateIntervalBox.Value = _config.CurrentValue.Performance.UpdateInterval / 60000;

        // Launch on startup
        _launchOnStartupCheckBox.Checked = IsStartupEnabled();

        // Performance settings
        _cacheDepthMaskCheckBox.Checked = _config.CurrentValue.Performance.CacheDepthMask;
        _enableDebugModeCheckBox.Checked = _config.CurrentValue.Performance.EnableDebugMode;
        _debugPathTextBox.Text = _config.CurrentValue.Performance.DebugPath;
        _debugPathTextBox.Enabled = _enableDebugModeCheckBox.Checked;

        // Depth settings
        _thresholdComboBox.SelectedIndex = _config.CurrentValue.Depth.Threshold == EDepthThresholdMode.Auto ? 0 : 1;
        _thresholdPercentileBox.Value = (decimal)_config.CurrentValue.Depth.ThresholdPercentile;
        _maskBlurBox.Value = (decimal)_config.CurrentValue.Depth.MaskBlur;

        // Clock style settings
        _fontFamilyComboBox.SelectedItem = _config.CurrentValue.Clock.Style.FontFamily;
        _fontStyleComboBox.SelectedItem = _config.CurrentValue.Clock.Style.FontStyle;
        _fontSizeBox.Value = (decimal)_config.CurrentValue.Clock.Style.FontSize;

        // Clock color
        _clockColorButton.Text = _config.CurrentValue.Clock.Style.Color;
        _clockColorButton.BackColor = ColorTranslator.FromHtml(_config.CurrentValue.Clock.Style.Color);

        // Shadow settings
        _shadowColorButton.Text = _config.CurrentValue.Clock.Style.ShadowColor;
        _shadowColorButton.BackColor = ColorTranslator.FromHtml(_config.CurrentValue.Clock.Style.ShadowColor);
        _shadowOpacityBox.Value = (decimal)_config.CurrentValue.Clock.Style.ShadowOpacity;
        _shadowBlurBox.Value = (decimal)_config.CurrentValue.Clock.Style.ShadowBlur;
        _shadowOffsetXBox.Value = (decimal)_config.CurrentValue.Clock.Style.ShadowOffset.X;
        _shadowOffsetYBox.Value = (decimal)_config.CurrentValue.Clock.Style.ShadowOffset.Y;

        // Adjust button text colors for readability
        var clockBrightness = (_clockColorButton.BackColor.R * 299 + _clockColorButton.BackColor.G * 587 + _clockColorButton.BackColor.B * 114) / 1000;
        _clockColorButton.ForeColor = clockBrightness > 128 ? Color.Black : Color.White;

        var shadowBrightness = (_shadowColorButton.BackColor.R * 299 + _shadowColorButton.BackColor.G * 587 + _shadowColorButton.BackColor.B * 114) / 1000;
        _shadowColorButton.ForeColor = shadowBrightness > 128 ? Color.Black : Color.White;
    }

    private bool _isApplyingSettings = false;
    private readonly string[] _spinnerChars = { ".", "..", "..." };
    private int _spinnerIndex = 0;

    private async Task ApplySettings()
    {
        if (_isApplyingSettings) return;
        _isApplyingSettings = true;

        var originalButtonText = _applyButton.Text;
        _applyButton.Enabled = false;
        _applyButton.Text = "Applying...";
        Cursor = Cursors.WaitCursor;

        _spinnerIndex = 0;
        var spinnerTimer = new System.Windows.Forms.Timer { Interval = 100 };
        spinnerTimer.Tick += (s, args) =>
        {
            _spinnerIndex = (_spinnerIndex + 1) % _spinnerChars.Length;
            _applyButton.Text = $"Applying{_spinnerChars[_spinnerIndex]}";
        };
        spinnerTimer.Start();

        try
        {
            Console.WriteLine("🔄 Applying settings via hot-reload...");

            // Capture selected values BEFORE config update
            bool isCustomMode = _modeComboBox.SelectedIndex == 0;
            string? newImagePath = null;

            if (isCustomMode)
            {
                newImagePath = _imageComboBox.SelectedItem?.ToString() ?? _imageComboBox.Text;
                if (!string.IsNullOrEmpty(newImagePath) && File.Exists(newImagePath))
                {
                    Console.WriteLine($"📁 Copying custom image to temp: {newImagePath}");
                    try
                    {
                        File.Copy(newImagePath, WallpaperPaths.CustomWallpaper, true);
                        Console.WriteLine($"✓ Custom image copied to: {WallpaperPaths.CustomWallpaper}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Failed to copy wallpaper: {ex.Message}");
                        MessageBox.Show($"Failed to copy wallpaper image: {ex.Message}", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
            }

            // Use hot-reload instead of restarting
            await _writableConfig.UpdateAsync(config =>
            {
                // Mode
                config.Wallpaper.Mode = isCustomMode ? EWallpaperMode.Custom : EWallpaperMode.Bing;

                // Clock settings
                config.Clock.Format = _timeFormatTextBox.Text;
                config.Clock.Position.AutoEnabled = _autoPositionCheckBox.Checked;
                config.Clock.Position.Strategy = (EPositionStrategy)_positionStrategyComboBox.SelectedIndex;
                config.Clock.Position.MaxCoveragePercent = _maxCoverageSlider.Value / 100f;
                config.Clock.Position.Horizontal = _horizontalSlider.Value / 100f;
                config.Clock.Position.Vertical = _verticalSlider.Value / 100f;

                // Clock style
                config.Clock.Style.FontFamily = _fontFamilyComboBox.SelectedItem?.ToString() ?? "Segoe UI";
                config.Clock.Style.FontStyle = _fontStyleComboBox.SelectedItem?.ToString() ?? "Bold";
                config.Clock.Style.FontSize = (float)_fontSizeBox.Value;
                config.Clock.Style.Color = _clockColorButton.Text;

                // Shadow settings
                config.Clock.Style.ShadowColor = _shadowColorButton.Text;
                config.Clock.Style.ShadowOpacity = (float)_shadowOpacityBox.Value;
                config.Clock.Style.ShadowBlur = (float)_shadowBlurBox.Value;
                config.Clock.Style.ShadowOffset.X = (float)_shadowOffsetXBox.Value;
                config.Clock.Style.ShadowOffset.Y = (float)_shadowOffsetYBox.Value;

                // Performance settings
                config.Performance.UpdateInterval = (int)_updateIntervalBox.Value * 60000;
                config.Performance.CacheDepthMask = _cacheDepthMaskCheckBox.Checked;
                config.Performance.EnableDebugMode = _enableDebugModeCheckBox.Checked;
                config.Performance.DebugPath = _debugPathTextBox.Text;

                // Depth settings
                config.Depth.Threshold = _thresholdComboBox.SelectedIndex == 0 ? EDepthThresholdMode.Auto : EDepthThresholdMode.Manual;
                config.Depth.ThresholdPercentile = (float)_thresholdPercentileBox.Value;
                config.Depth.MaskBlur = (float)_maskBlurBox.Value;

                if (isCustomMode)
                {
                    // Custom image mode - update path in config
                    if (!string.IsNullOrEmpty(newImagePath) && File.Exists(newImagePath))
                    {
                        config.Wallpaper.Path = newImagePath;
                        Console.WriteLine($"📁 Custom wallpaper path saved to config: {newImagePath}");
                    }
                }
                else
                {
                    // Bing wallpaper mode
                    config.Wallpaper.Path = "";
                    Console.WriteLine("🖼️ Switched to Bing wallpaper mode");
                }

                // Update startup setting
                SetStartupEnabled(_launchOnStartupCheckBox.Checked);
            });

            // Force wallpaper reload after config update
            await Task.Run(() =>
            {
                try
                {
                    // Small delay to ensure config is fully saved
                    Thread.Sleep(100);
                    _orchestrator.UpdateWallpaper();
                    Console.WriteLine("✓ Wallpaper reloaded after settings change");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to reload wallpaper after settings: {ex.Message}");
                }
            });

            // Show success message
            MessageBox.Show("Settings applied successfully! Changes are now active.",
                "Settings Applied", MessageBoxButtons.OK, MessageBoxIcon.Information);

            Console.WriteLine("✅ Settings applied via hot-reload");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to apply settings: {ex.Message}",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Console.WriteLine($"❌ Settings application failed: {ex.Message}");
        }
        finally
        {
            spinnerTimer?.Stop();
            spinnerTimer?.Dispose();
            _applyButton.Enabled = true;
            _applyButton.Text = originalButtonText;
            Cursor = Cursors.Default;
            _isApplyingSettings = false;
        }
    }

    private void ShowSettings()
    {
        Show();
        WindowState = FormWindowState.Normal;
        ShowInTaskbar = true;
        BringToFront();
        Focus();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            WindowState = FormWindowState.Minimized;
            ShowInTaskbar = false;
            Visible = false;
        }
        base.OnFormClosing(e);
    }

    private void ExitApplication()
    {
        _trayIcon.Visible = false;
        Application.Exit();
    }


    private bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
            var value = key?.GetValue("DepthClockWallpaper");
            return value != null;
        }
        catch
        {
            return false;
        }
    }

    private void SetStartupEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

            if (enabled)
            {
                key?.SetValue("DepthClockWallpaper", Application.ExecutablePath);
            }
            else
            {
                key?.DeleteValue("DepthClockWallpaper", false);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to update startup setting: {ex.Message}",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trayIcon?.Dispose();
            _bingUpdateTimer?.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Loads the application icon, trying multiple sources to avoid blocking file I/O.
    /// </summary>
    private static Icon LoadApplicationIcon()
    {
        try
        {
            // First try to extract from the executable (fastest, no file I/O for separate file)
            var exeIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (exeIcon != null)
                return exeIcon;
        }
        catch
        {
            // Ignore and try fallback
        }

        try
        {
            // Fallback to icon.ico file if it exists
            if (File.Exists("icon.ico"))
                return new Icon("icon.ico");
        }
        catch
        {
            // Ignore and use system default
        }

        // Last resort: use system application icon
        return SystemIcons.Application;
    }

    private static string[] GetSystemFonts()
    {
        try
        {
            var fontFamilies = FontFamily.Families
                .Select(f => f.Name)
                .OrderBy(name => name)
                .ToArray();

            return fontFamilies.Length > 0 ? fontFamilies : new[] { "Segoe UI", "Arial", "Times New Roman" };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load system fonts: {ex.Message}");
            return new[] { "Segoe UI", "Arial", "Times New Roman" };
        }
    }

    /// <summary>
    /// Loads system fonts asynchronously to prevent UI freeze during startup.
    /// </summary>
    private async Task LoadFontsAsync()
    {
        try
        {
            var fonts = await Task.Run(() => GetSystemFonts()).ConfigureAwait(false);

            if (InvokeRequired)
            {
                Invoke(() => PopulateFontComboBox(fonts));
            }
            else
            {
                PopulateFontComboBox(fonts);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load fonts asynchronously: {ex.Message}");
        }
    }

    private void PopulateFontComboBox(string[] fonts)
    {
        var currentSelection = _config.CurrentValue.Clock.Style.FontFamily;

        _fontFamilyComboBox.Items.Clear();
        _fontFamilyComboBox.Items.AddRange(fonts);

        // Restore the configured font selection
        var index = _fontFamilyComboBox.Items.IndexOf(currentSelection);
        if (index >= 0)
        {
            _fontFamilyComboBox.SelectedIndex = index;
        }
        else if (_fontFamilyComboBox.Items.Count > 0)
        {
            // Default to first font if configured font not found
            _fontFamilyComboBox.SelectedIndex = 0;
        }
    }
}