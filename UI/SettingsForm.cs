using SkiaSharp;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Diagnostics;
using DepthClockWallpaper.Core;
using Microsoft.Win32;
using DepthClockWallpaper.Models;

namespace DepthClockWallpaper.UI;

public partial class SettingsForm : Form
{
    private readonly HotWallpaperOrchestrator _orchestrator;
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

    // Debug settings controls
    private CheckBox _enableDebugModeCheckBox;
    private TextBox _debugPathTextBox;

    public SettingsForm()
    {
        _orchestrator = new HotWallpaperOrchestrator();
        InitializeComponent();
        InitializeTrayIcon();
        LoadSettingsToUI();

        // Initialize orchestrator with current wallpaper if available
        var currentConfig = HotConfigManager.Current;
        // Load wallpaper based on mode
        Task.Run(() =>
        {
            try
            {
                _orchestrator.LoadWallpaper();
                _orchestrator.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to load initial wallpaper: {ex.Message}");
            }
        });

        // Hide the main settings window initially
        WindowState = FormWindowState.Minimized;
        ShowInTaskbar = false;
        Visible = false;
    }

    private void InitializeComponent()
    {
        // Form setup
        Text = "DepthClockWallpaper Settings";
        Size = new Size(700, 850);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = new Icon("icon.ico");
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
            Value = decimal.Round((_orchestrator.CurrentConfig.Performance.UpdateInterval / 60000), 2),
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
            Text = _orchestrator.CurrentConfig.Clock.Format
        };
        positionLayout.Controls.Add(_timeFormatTextBox, 1, 0);

        _horizontalLabel = CreateLabel("Horizontal: 50%");
        positionLayout.Controls.Add(_horizontalLabel, 0, 1);
        _horizontalSlider = new TrackBar
        {
            Minimum = 0,
            Maximum = 100,
            TickFrequency = 10,
            Width = 400,
            Value = (int)(_orchestrator.CurrentConfig.Clock.Position.Horizontal * 100)
        };
        _horizontalSlider.ValueChanged += (s, e) => _horizontalLabel.Text = $"Horizontal: {_horizontalSlider.Value}%";
        positionLayout.Controls.Add(_horizontalSlider, 1, 1);

        _verticalLabel = CreateLabel("Vertical: 50%");
        positionLayout.Controls.Add(_verticalLabel, 0, 2);
        _verticalSlider = new TrackBar
        {
            Minimum = 0,
            Maximum = 100,
            TickFrequency = 10,
            Width = 400,
            Value = (int)(_orchestrator.CurrentConfig.Clock.Position.Vertical * 100)
        };
        _verticalSlider.ValueChanged += (s, e) => _verticalLabel.Text = $"Vertical: {_verticalSlider.Value}%";
        positionLayout.Controls.Add(_verticalSlider, 1, 2);

        positionGroup.Controls.Add(positionLayout);
        mainPanel.Controls.Add(positionGroup);

        // === CLOCK STYLE SECTION ===
        var styleGroup = CreateGroupBox("Clock Style");
        var styleLayout = CreateFormLayout();

        styleLayout.Controls.Add(CreateLabel("Font Family:"), 0, 0);
        var availableFonts = GetSystemFonts();
        _fontFamilyComboBox = CreateComboBox(availableFonts);
        styleLayout.Controls.Add(_fontFamilyComboBox, 1, 0);

        styleLayout.Controls.Add(CreateLabel("Font Style:"), 0, 1);
        _fontStyleComboBox = CreateComboBox(new[] { "Regular", "Bold", "Italic", "Bold Italic" });
        styleLayout.Controls.Add(_fontStyleComboBox, 1, 1);

        styleLayout.Controls.Add(CreateLabel("Clock Color:"), 0, 2);
        _clockColorButton = CreateColorButton("#FFFFFF", Color.White, Color.Black);
        _clockColorButton.Click += (s, e) => ShowColorDialog(_clockColorButton);
        styleLayout.Controls.Add(_clockColorButton, 1, 2);

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
        _applyButton.Click += ApplySettings;
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

        ModeChanged(null, null);
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
            Icon = new Icon("icon.ico"),
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
                if (path == _orchestrator.CurrentConfig.Wallpaper.Path)
                    _imageComboBox.SelectedItem = path;
            }
        }

        if (_imageComboBox.SelectedIndex == -1 && !string.IsNullOrEmpty(_orchestrator.CurrentConfig.Wallpaper.Path))
        {
            _imageComboBox.Text = _orchestrator.CurrentConfig.Wallpaper.Path;
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
        try
        {
            var bingService = new BingWallpaperService();
            var latestImage = await bingService.GetLatestImageAsync();

            if (latestImage != null)
            {
                _lastBingUpdateLabel.Text = $"Updated: {latestImage.Date:yyyy-MM-dd HH:mm}";
                _lastBingUpdateLabel.ForeColor = Color.Green;

                // Check if we're in Bing mode and need to reload
                if (_orchestrator.CurrentConfig.Wallpaper.Mode == EWallpaperMode.Bing)
                {
                    Console.WriteLine("Bing image updated, reloading wallpaper...");
                    _orchestrator.LoadWallpaper();
                }
            }
        }
        catch (Exception ex)
        {
            _lastBingUpdateLabel.Text = $"Error: {ex.Message}";
            _lastBingUpdateLabel.ForeColor = Color.Red;
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
        _modeComboBox.SelectedIndex = _orchestrator.CurrentConfig.Wallpaper.Mode == EWallpaperMode.Custom ? 0 : 1;

        // Time format
        _timeFormatTextBox.Text = _orchestrator.CurrentConfig.Clock.Format;

        // Position
        _horizontalSlider.Value = (int)(_orchestrator.CurrentConfig.Clock.Position.Horizontal * 100);
        _verticalSlider.Value = (int)(_orchestrator.CurrentConfig.Clock.Position.Vertical * 100);

        // Update interval
        _updateIntervalBox.Value = _orchestrator.CurrentConfig.Performance.UpdateInterval / 60000;

        // Launch on startup
        _launchOnStartupCheckBox.Checked = IsStartupEnabled();

        // Performance settings
        _cacheDepthMaskCheckBox.Checked = _orchestrator.CurrentConfig.Performance.CacheDepthMask;
        _enableDebugModeCheckBox.Checked = _orchestrator.CurrentConfig.Performance.EnableDebugMode;
        _debugPathTextBox.Text = _orchestrator.CurrentConfig.Performance.DebugPath;
        _debugPathTextBox.Enabled = _enableDebugModeCheckBox.Checked;

        // Depth settings
        _thresholdComboBox.SelectedIndex = _orchestrator.CurrentConfig.Depth.Threshold == "auto" ? 0 : 1;
        _thresholdPercentileBox.Value = (decimal)_orchestrator.CurrentConfig.Depth.ThresholdPercentile;
        _maskBlurBox.Value = (decimal)_orchestrator.CurrentConfig.Depth.MaskBlur;

        // Clock style settings
        _fontFamilyComboBox.SelectedItem = _orchestrator.CurrentConfig.Clock.Style.FontFamily;
        _fontStyleComboBox.SelectedItem = _orchestrator.CurrentConfig.Clock.Style.FontStyle;

        // Clock color
        _clockColorButton.Text = _orchestrator.CurrentConfig.Clock.Style.Color;
        _clockColorButton.BackColor = ColorTranslator.FromHtml(_orchestrator.CurrentConfig.Clock.Style.Color);

        // Shadow settings
        _shadowColorButton.Text = _orchestrator.CurrentConfig.Clock.Style.ShadowColor;
        _shadowColorButton.BackColor = ColorTranslator.FromHtml(_orchestrator.CurrentConfig.Clock.Style.ShadowColor);
        _shadowOpacityBox.Value = (decimal)_orchestrator.CurrentConfig.Clock.Style.ShadowOpacity;
        _shadowBlurBox.Value = (decimal)_orchestrator.CurrentConfig.Clock.Style.ShadowBlur;
        _shadowOffsetXBox.Value = (decimal)_orchestrator.CurrentConfig.Clock.Style.ShadowOffset.X;
        _shadowOffsetYBox.Value = (decimal)_orchestrator.CurrentConfig.Clock.Style.ShadowOffset.Y;

        // Adjust button text colors for readability
        var clockBrightness = (_clockColorButton.BackColor.R * 299 + _clockColorButton.BackColor.G * 587 + _clockColorButton.BackColor.B * 114) / 1000;
        _clockColorButton.ForeColor = clockBrightness > 128 ? Color.Black : Color.White;

        var shadowBrightness = (_shadowColorButton.BackColor.R * 299 + _shadowColorButton.BackColor.G * 587 + _shadowColorButton.BackColor.B * 114) / 1000;
        _shadowColorButton.ForeColor = shadowBrightness > 128 ? Color.Black : Color.White;
    }

    private bool _isApplyingSettings = false;
    private readonly char[] _spinnerChars = { '|', '/', '-', '\\' };
    private int _spinnerIndex = 0;

    private void ApplySettings(object? sender, EventArgs e)
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
            _applyButton.Text = $"Applying... {_spinnerChars[_spinnerIndex]}";
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
            HotConfigManager.UpdateConfig(config =>
            {
                // Mode
                config.Wallpaper.Mode = isCustomMode ? EWallpaperMode.Custom : EWallpaperMode.Bing;

                // Clock settings
                config.Clock.Format = _timeFormatTextBox.Text;
                config.Clock.Position.Horizontal = _horizontalSlider.Value / 100f;
                config.Clock.Position.Vertical = _verticalSlider.Value / 100f;

                // Clock style
                config.Clock.Style.FontFamily = _fontFamilyComboBox.SelectedItem?.ToString() ?? "Segoe UI";
                config.Clock.Style.FontStyle = _fontStyleComboBox.SelectedItem?.ToString() ?? "Bold";
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
                config.Depth.Threshold = _thresholdComboBox.SelectedIndex == 0 ? "auto" : "manual";
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
            Task.Run(() =>
            {
                try
                {
                    // Small delay to ensure config is fully saved
                    System.Threading.Thread.Sleep(100);
                    _orchestrator.LoadWallpaper();
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
            Console.WriteLine($"⚠️ Failed to load system fonts: {ex.Message}");
            return new[] { "Segoe UI", "Arial", "Times New Roman" };
        }
    }
}