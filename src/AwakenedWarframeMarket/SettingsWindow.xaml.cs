using System.Windows;
using System.Windows.Input;
using AwakenedWarframeMarket.Models;
using AwakenedWarframeMarket.Services;

namespace AwakenedWarframeMarket;

public partial class SettingsWindow : Window
{
    private readonly Settings _settings;
    private readonly SettingsManager _settingsManager;

    private ModifierKeys _modifiers;
    private Key _key;
    private bool _capturing;

    public SettingsWindow(Settings settings, SettingsManager settingsManager)
    {
        InitializeComponent();

        _settings = settings;
        _settingsManager = settingsManager;

        _modifiers = HotkeyManager.ParseModifier(settings.HotkeyModifier);
        _key = HotkeyManager.ParseKey(settings.HotkeyKey);
        OnlyInWarframeCheckBox.IsChecked = settings.OnlyInWarframe;
        DebugModeCheckBox.IsChecked = settings.DebugMode;

        ShowHotkey();
    }

    // ---- Hotkey capture --------------------------------------------------------------

    private void HotkeyBox_Click(object sender, RoutedEventArgs e)
    {
        _capturing = true;
        HotkeyBoxText.Text = "Press a key combination…";
        HotkeyBoxText.Foreground = (System.Windows.Media.Brush)FindResource("Muted");
        ShowHint("Esc to keep the current one");
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_capturing)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                e.Handled = true;
            }
            return;
        }

        e.Handled = true;

        // Alt combinations arrive as Key.System
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift);

        if (key == Key.Escape)
        {
            StopCapture(null);
            return;
        }

        // Only modifiers held so far: show them and wait for the main key
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin)
        {
            var held = HotkeyManager.ModifierToString(modifiers);
            HotkeyBoxText.Text = string.IsNullOrEmpty(held) ? "Press a key combination…" : held.Replace("+", " + ") + " + …";
            return;
        }

        bool isLetterOrDigit = key is >= Key.A and <= Key.Z or >= Key.D0 and <= Key.D9;
        bool isFunctionKey = key is >= Key.F1 and <= Key.F12;

        if (!isLetterOrDigit && !isFunctionKey)
        {
            ShowHint("Use a letter, a digit or an F-key");
            return;
        }

        // A plain letter or digit would fire while typing in chat; require a modifier for those
        if (isLetterOrDigit && modifiers == ModifierKeys.None)
        {
            ShowHint("Add Ctrl, Alt or Shift so the hotkey doesn't fire while you type");
            return;
        }

        _modifiers = modifiers;
        _key = key;
        StopCapture(null);
    }

    private void StopCapture(string? hint)
    {
        _capturing = false;
        ShowHotkey();
        ShowHint(hint);
    }

    private void ShowHotkey()
    {
        HotkeyBoxText.Text = HotkeyManager.Display(_modifiers, _key);
        HotkeyBoxText.Foreground = (System.Windows.Media.Brush)FindResource("AccentText");
    }

    private void ShowHint(string? text)
    {
        HotkeyHint.Text = text ?? "";
        HotkeyHint.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    // ---- Save / cancel ---------------------------------------------------------------

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.HotkeyModifier = HotkeyManager.ModifierToSetting(_modifiers);
        _settings.HotkeyKey = _key.ToString();
        _settings.OnlyInWarframe = OnlyInWarframeCheckBox.IsChecked ?? true;
        _settings.DebugMode = DebugModeCheckBox.IsChecked ?? false;

        _settingsManager.SaveSettings(_settings);

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
