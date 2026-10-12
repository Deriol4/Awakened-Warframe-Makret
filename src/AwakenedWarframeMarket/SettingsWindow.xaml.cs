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

        var error = HotkeyManager.ValidationError(modifiers, key);
        if (error != null)
        {
            ShowHint(error);
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
        var updated = new Settings
        {
            Version = Settings.CurrentVersion,
            HotkeyModifier = HotkeyManager.ModifierToSetting(_modifiers),
            HotkeyKey = _key.ToString(),
            OnlyInWarframe = OnlyInWarframeCheckBox.IsChecked ?? true
        };

        // Keep the dialog open if the file can't be written, instead of silently losing the change
        var error = _settingsManager.SaveSettings(updated);
        if (error != null)
        {
            ShowHint(error);
            return;
        }

        _settings.Version = updated.Version;
        _settings.HotkeyModifier = updated.HotkeyModifier;
        _settings.HotkeyKey = updated.HotkeyKey;
        _settings.OnlyInWarframe = updated.OnlyInWarframe;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
