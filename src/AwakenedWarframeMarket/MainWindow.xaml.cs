using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using AwakenedWarframeMarket.Models;
using AwakenedWarframeMarket.Services;

namespace AwakenedWarframeMarket;

public partial class MainWindow : Window
{
    private readonly SettingsManager _settingsManager;
    private readonly HotkeyManager _hotkeyManager;
    private readonly OCRService _ocrService;
    private readonly WarframeMarketAPI _marketApi;
    private readonly ForegroundWatcher _foreground = new();
    private PriceCheckService? _priceCheckService;
    private Settings _settings;
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private System.Drawing.Icon? _trayIcon;

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    private bool _ready;               // OCR initialised, price checks possible
    private bool _hotkeyUnavailable;   // another app owns the key combination
    private bool _editingSettings;     // settings window open: keep the hotkey free so it can be captured
    private string? _startupError;

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
        StateChanged += MainWindow_StateChanged;

        SetupTrayIcon();
        UpdateHotkeyDisplay();

        var version = typeof(MainWindow).Assembly.GetName().Version;
        if (version != null)
            FooterText.Text = $"v{version.ToString(3)} · warframe.market · PC";
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _hotkeyManager.Initialize(this);
        UpdateStatus();

        // Fetch the item catalogue in the background while OCR starts up
        _ = _marketApi.PreloadAsync();

        try
        {
            await _ocrService.InitializeAsync();

            var screenCapture = new ScreenCapture();
            _priceCheckService = new PriceCheckService(screenCapture, _ocrService, _marketApi);
            _ready = true;

            // Take the hotkey only while Warframe (or this app) is in front
            _foreground.Changed += UpdateHotkeyRegistration;
            _foreground.Start();
            UpdateHotkeyRegistration();
        }
        catch (Exception ex)
        {
            _startupError = DescribeStartupError(ex);
            UpdateStatus();
        }
    }

    // Tesseract needs its native DLLs, and those need the Visual C++ runtime; without it the load fails
    private static string DescribeStartupError(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is DllNotFoundException or BadImageFormatException ||
                e.Message.Contains("Failed to find library", StringComparison.OrdinalIgnoreCase))
                return "Install the Microsoft Visual C++ Redistributable (x64) and restart the app.";
        }
        return ex.Message;
    }

    // ---- Hotkey ----------------------------------------------------------------------

    private ModifierKeysAndKey CurrentHotkey() =>
        new(HotkeyManager.ParseModifier(_settings.HotkeyModifier), HotkeyManager.ParseKey(_settings.HotkeyKey));

    private readonly record struct ModifierKeysAndKey(System.Windows.Input.ModifierKeys Modifiers, System.Windows.Input.Key Key);

    private bool ShouldListen =>
        _ready && !_editingSettings &&
        (!_settings.OnlyInWarframe || _foreground.WarframeInFront || _foreground.OwnWindowInFront);

    /// <summary>Register or release the global hotkey depending on what's in front.</summary>
    private void UpdateHotkeyRegistration()
    {
        if (ShouldListen)
        {
            if (!_hotkeyManager.IsRegistered)
            {
                var hk = CurrentHotkey();
                _hotkeyUnavailable = !_hotkeyManager.RegisterHotkey(hk.Modifiers, hk.Key, OnHotkeyPressed);
            }
        }
        else
        {
            _hotkeyManager.Unregister();
        }

        UpdateStatus();
    }

    private void OnHotkeyPressed()
    {
        if (_priceCheckService != null)
            _ = _priceCheckService.CheckPriceAsync();
    }

    private void UpdateHotkeyDisplay()
    {
        var hk = CurrentHotkey();
        HotkeyText.Text = HotkeyManager.Display(hk.Modifiers, hk.Key);
    }

    // ---- Status card ------------------------------------------------------------------

    private void UpdateStatus()
    {
        var hotkey = HotkeyText.Text;

        if (_startupError != null)
            SetStatus("Danger", "Couldn't start", $"Text recognition failed to load. {_startupError}");
        else if (!_ready)
            SetStatus("Muted", "Starting…", "Preparing text recognition");
        else if (_hotkeyUnavailable)
            SetStatus("Danger", "Hotkey unavailable", $"{hotkey} is taken by another app. Pick another one in Settings.");
        else if (!_settings.OnlyInWarframe)
            SetStatus("InGame", "Ready", $"{hotkey} works in any window");
        else if (_foreground.WarframeInFront || _foreground.OwnWindowInFront)
            SetStatus("InGame", "Ready", $"Hover an item in Warframe and press {hotkey}");
        else
            SetStatus("Accent", "Waiting for Warframe", $"{hotkey} works while Warframe is in front");
    }

    private void SetStatus(string dotBrushKey, string title, string detail)
    {
        StatusDot.Fill = (Brush)FindResource(dotBrushKey);
        StatusTitle.Text = title;
        StatusDetail.Text = detail;
    }

    // ---- Buttons -------------------------------------------------------------------

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        // Release the hotkey while editing so the settings window can capture the same combination
        _editingSettings = true;
        _hotkeyManager.Unregister();

        try
        {
            var settingsWindow = new SettingsWindow(_settings, _settingsManager) { Owner = this };
            if (settingsWindow.ShowDialog() == true)
                _settings = _settingsManager.LoadSettings();
        }
        finally
        {
            _editingSettings = false;
        }

        UpdateHotkeyDisplay();
        _hotkeyUnavailable = false;
        UpdateHotkeyRegistration();
    }

    // Minimizing (title bar button or taskbar) hides the app to the tray
    private void MinimizeWindow_Click(object sender, RoutedEventArgs e) => Hide();

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
            Hide();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => Hide();

    private void SetupTrayIcon()
    {
        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "Awakened Warframe Market",
            Visible = true
        };

        // Simple generated icon: gold square with a "W"
        using (var bitmap = new System.Drawing.Bitmap(32, 32))
        {
            using (var g = System.Drawing.Graphics.FromImage(bitmap))
            using (var font = new System.Drawing.Font("Arial", 18, System.Drawing.FontStyle.Bold))
            {
                g.Clear(System.Drawing.Color.FromArgb(200, 167, 90));
                g.DrawString("W", font, System.Drawing.Brushes.Black, 4, 2);
            }

            // GetHicon creates a native handle that must be freed; the clone owns its own copy
            var handle = bitmap.GetHicon();
            try
            {
                using var temp = System.Drawing.Icon.FromHandle(handle);
                _trayIcon = (System.Drawing.Icon)temp.Clone();
            }
            finally
            {
                DestroyIcon(handle);
            }
        }
        _notifyIcon.Icon = _trayIcon;

        _notifyIcon.DoubleClick += (s, e) => ShowFromTray();

        var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        contextMenu.Items.Add("Open", null, (s, e) => ShowFromTray());
        contextMenu.Items.Add("-");
        contextMenu.Items.Add("Exit", null, (s, e) =>
        {
            _notifyIcon.Visible = false;
            Application.Current.Shutdown();
        });

        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    public void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _foreground.Dispose();
        _hotkeyManager.Dispose();
        _ocrService.Dispose();
        _notifyIcon?.Dispose();
        _trayIcon?.Dispose();
    }
}
