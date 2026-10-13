using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AwakenedWarframeMarket.Models;
using AwakenedWarframeMarket.Services;

namespace AwakenedWarframeMarket;

public partial class SettingsWindow : Window
{
    private readonly Settings _settings;
    private readonly SettingsManager _settingsManager;

    // One editable combination per hotkey field
    private sealed class HotkeyField
    {
        public required TextBlock Text { get; init; }
        public required TextBlock Hint { get; init; }
        public ModifierKeys Modifiers { get; set; }
        public Key Key { get; set; }
    }

    private readonly AppLanguage _uiLanguageAtStart;
    private AppLanguage _uiLanguage;
    private AppLanguage _gameLanguage;

    private readonly HotkeyField _price;
    private readonly HotkeyField _search;
    private HotkeyField? _capturing;

    public SettingsWindow(Settings settings, SettingsManager settingsManager)
    {
        InitializeComponent();

        _settings = settings;
        _settingsManager = settingsManager;

        _price = new HotkeyField
        {
            Text = HotkeyBoxText,
            Hint = HotkeyHint,
            Modifiers = HotkeyManager.ParseModifier(settings.HotkeyModifier),
            Key = HotkeyManager.ParseKey(settings.HotkeyKey)
        };
        _search = new HotkeyField
        {
            Text = SearchHotkeyBoxText,
            Hint = SearchHotkeyHint,
            Modifiers = HotkeyManager.ParseModifier(settings.SearchHotkeyModifier, HotkeyManager.DefaultSearchModifiers),
            Key = HotkeyManager.ParseKey(settings.SearchHotkeyKey, HotkeyManager.DefaultSearchKey)
        };
        OnlyInWarframeCheckBox.IsChecked = settings.OnlyInWarframe;

        _uiLanguageAtStart = _uiLanguage = L.Resolve(settings.Language);
        _gameLanguage = string.IsNullOrEmpty(settings.GameLanguage) ? _uiLanguage : L.Resolve(settings.GameLanguage);
        ShowLanguages();

        ShowHotkey(_price);
        ShowHotkey(_search);
    }

    // ---- Hotkey capture --------------------------------------------------------------

    private void HotkeyBox_Click(object sender, RoutedEventArgs e) => StartCapture(_price);

    private void SearchHotkeyBox_Click(object sender, RoutedEventArgs e) => StartCapture(_search);

    private void StartCapture(HotkeyField field)
    {
        if (_capturing != null && _capturing != field)
            StopCapture(null);

        _capturing = field;
        field.Text.Text = L.PressCombination;
        field.Text.Foreground = (System.Windows.Media.Brush)FindResource("Muted");
        ShowHint(field, L.EscKeepsCurrent);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var field = _capturing;
        if (field == null)
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
            field.Text.Text = string.IsNullOrEmpty(held) ? L.PressCombination : held.Replace("+", " + ") + " + …";
            return;
        }

        var error = HotkeyManager.ValidationError(modifiers, key);
        var other = field == _price ? _search : _price;
        if (error == null && other.Modifiers == modifiers && other.Key == key)
            error = field == _price ? L.AlreadySearchHotkey : L.AlreadyPriceHotkey;
        if (error != null)
        {
            ShowHint(field, error);
            return;
        }

        field.Modifiers = modifiers;
        field.Key = key;
        StopCapture(null);
    }

    private void StopCapture(string? hint)
    {
        if (_capturing == null)
            return;
        var field = _capturing;
        _capturing = null;
        ShowHotkey(field);
        ShowHint(field, hint);
    }

    private void ShowHotkey(HotkeyField field)
    {
        field.Text.Text = HotkeyManager.Display(field.Modifiers, field.Key);
        field.Text.Foreground = (System.Windows.Media.Brush)FindResource("AccentText");
    }

    private static void ShowHint(HotkeyField field, string? text)
    {
        field.Hint.Text = text ?? "";
        field.Hint.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    // ---- Languages ---------------------------------------------------------------------

    private void UiLanguage_Click(object sender, RoutedEventArgs e)
    {
        _uiLanguage = sender == UiRussianButton ? AppLanguage.Russian : AppLanguage.English;
        ShowLanguages();
    }

    private void GameLanguage_Click(object sender, RoutedEventArgs e)
    {
        _gameLanguage = sender == GameRussianButton ? AppLanguage.Russian : AppLanguage.English;
        ShowLanguages();
    }

    private void ShowLanguages()
    {
        StyleChoice(UiEnglishButton, _uiLanguage == AppLanguage.English);
        StyleChoice(UiRussianButton, _uiLanguage == AppLanguage.Russian);
        StyleChoice(GameEnglishButton, _gameLanguage == AppLanguage.English);
        StyleChoice(GameRussianButton, _gameLanguage == AppLanguage.Russian);
        UiLanguageHint.Visibility = _uiLanguage != _uiLanguageAtStart ? Visibility.Visible : Visibility.Collapsed;
    }

    private void StyleChoice(Button button, bool selected)
    {
        button.BorderBrush = (System.Windows.Media.Brush)FindResource(selected ? "Accent" : "ControlBorder");
        button.Background = selected ? (System.Windows.Media.Brush)FindResource("AccentBg") : System.Windows.Media.Brushes.Transparent;
        button.Foreground = (System.Windows.Media.Brush)FindResource(selected ? "AccentText" : "Soft");
    }

    // ---- Save / cancel ---------------------------------------------------------------

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        StopCapture(null);

        var updated = new Settings
        {
            Version = Settings.CurrentVersion,
            HotkeyModifier = HotkeyManager.ModifierToSetting(_price.Modifiers),
            HotkeyKey = _price.Key.ToString(),
            SearchHotkeyModifier = HotkeyManager.ModifierToSetting(_search.Modifiers),
            SearchHotkeyKey = _search.Key.ToString(),
            OnlyInWarframe = OnlyInWarframeCheckBox.IsChecked ?? true,
            Language = L.ToSetting(_uiLanguage),
            GameLanguage = L.ToSetting(_gameLanguage)
        };

        // Keep the dialog open if the file can't be written, instead of silently losing the change
        var error = _settingsManager.SaveSettings(updated);
        if (error != null)
        {
            ShowHint(_price, error);
            return;
        }

        _settings.Version = updated.Version;
        _settings.HotkeyModifier = updated.HotkeyModifier;
        _settings.HotkeyKey = updated.HotkeyKey;
        _settings.SearchHotkeyModifier = updated.SearchHotkeyModifier;
        _settings.SearchHotkeyKey = updated.SearchHotkeyKey;
        _settings.OnlyInWarframe = updated.OnlyInWarframe;
        _settings.Language = updated.Language;
        _settings.GameLanguage = updated.GameLanguage;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
