using System;
using System.Windows;
using System.Windows.Input;
using AwakenedWarframeMarket.Models;
using AwakenedWarframeMarket.Services;

namespace AwakenedWarframeMarket;

public partial class MainWindow : Window
{
    private readonly SettingsManager _settingsManager;
    private readonly HotkeyManager _hotkeyManager;
    private readonly OCRService _ocrService;
    private readonly WarframeMarketAPI _marketApi;
    private PriceCheckService? _priceCheckService;
    private Settings _settings;
    private System.Windows.Forms.NotifyIcon? _notifyIcon;

    public MainWindow()
    {
        InitializeComponent();

        _settingsManager = new SettingsManager();
        _hotkeyManager = new HotkeyManager();
        _ocrService = new OCRService();
        _marketApi = new WarframeMarketAPI();
        _settings = _settingsManager.LoadSettings();

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;

        SetupTrayIcon();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Initialize hotkey manager
        _hotkeyManager.Initialize(this);

        // Show loading status
        StatusText.Text = "Initializing...";

        try
        {
            // Initialize OCR
            await _ocrService.InitializeAsync();

            // Create price check service
            var screenCapture = new ScreenCapture(_settings.DebugMode);
            _priceCheckService = new PriceCheckService(screenCapture, _ocrService, _marketApi);

            // Register hotkey
            RegisterCurrentHotkey();

            StatusText.Text = "Ready";
            UpdateHotkeyDisplay();

            // Show startup message
            MessageBox.Show(
                $"Awakened Warframe Market started!\n\nPress {GetHotkeyDisplayString()} to check prices.\n\nHover your cursor over an item name in Warframe, then press the hotkey.",
                "Awakened Warframe Market",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Error";
            StatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 100, 100));
            MessageBox.Show($"Initialization error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RegisterCurrentHotkey()
    {
        var modifier = HotkeyManager.ParseModifier(_settings.HotkeyModifier);
        var key = HotkeyManager.ParseKey(_settings.HotkeyKey);

        var success = _hotkeyManager.RegisterHotkey(modifier, key, OnHotkeyPressed);

        if (!success)
        {
            MessageBox.Show(
                $"Failed to register hotkey {GetHotkeyDisplayString()}.\nIt may be in use by another application.",
                "Hotkey Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnHotkeyPressed()
    {
        if (_priceCheckService != null)
        {
            _ = _priceCheckService.CheckPriceAsync();
        }
    }

    private void UpdateHotkeyDisplay()
    {
        HotkeyText.Text = $"Press {GetHotkeyDisplayString()} to check prices";
    }

    private string GetHotkeyDisplayString()
    {
        var modifier = HotkeyManager.ParseModifier(_settings.HotkeyModifier);
        var key = HotkeyManager.ParseKey(_settings.HotkeyKey);
        return $"{HotkeyManager.ModifierToString(modifier)}+{key}";
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(_settings, _settingsManager);
        settingsWindow.Owner = this;

        if (settingsWindow.ShowDialog() == true)
        {
            _settings = _settingsManager.LoadSettings();
            RegisterCurrentHotkey();
            UpdateHotkeyDisplay();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        _notifyIcon?.ShowBalloonTip(2000, "Awakened Warframe Market",
            $"Running in background. Press {GetHotkeyDisplayString()} to check prices.",
            System.Windows.Forms.ToolTipIcon.Info);
    }

    private void SetupTrayIcon()
    {
        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "Awakened Warframe Market",
            Visible = true
        };

        // Create icon from text (simple colored square)
        using var bitmap = new System.Drawing.Bitmap(32, 32);
        using var g = System.Drawing.Graphics.FromImage(bitmap);
        g.Clear(System.Drawing.Color.FromArgb(212, 175, 55)); // Gold color
        g.DrawString("W", new System.Drawing.Font("Arial", 18, System.Drawing.FontStyle.Bold),
            System.Drawing.Brushes.Black, 4, 2);
        _notifyIcon.Icon = System.Drawing.Icon.FromHandle(bitmap.GetHicon());

        _notifyIcon.DoubleClick += (s, e) =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        };

        var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        contextMenu.Items.Add("Open", null, (s, e) =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        });
        contextMenu.Items.Add("-");
        contextMenu.Items.Add("Exit", null, (s, e) =>
        {
            _notifyIcon.Visible = false;
            Application.Current.Shutdown();
        });

        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _hotkeyManager.Dispose();
        _ocrService.Dispose();
        _notifyIcon?.Dispose();
    }

    public void UpdateSettings(Settings newSettings)
    {
        _settings = newSettings;
        RegisterCurrentHotkey();
        UpdateHotkeyDisplay();
    }
}
