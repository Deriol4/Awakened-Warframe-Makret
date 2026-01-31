using System;
using System.Windows;
using System.Windows.Controls;
using AwakenedWarframeMarket.Models;
using AwakenedWarframeMarket.Services;

namespace AwakenedWarframeMarket;

public partial class SettingsWindow : Window
{
    private readonly Settings _settings;
    private readonly SettingsManager _settingsManager;

    public SettingsWindow(Settings settings, SettingsManager settingsManager)
    {
        InitializeComponent();

        _settings = settings;
        _settingsManager = settingsManager;

        PopulateKeyComboBox();
        LoadCurrentSettings();

        ModifierComboBox.SelectionChanged += OnSelectionChanged;
        KeyComboBox.SelectionChanged += OnSelectionChanged;
    }

    private void PopulateKeyComboBox()
    {
        // Add letter keys A-Z
        for (char c = 'A'; c <= 'Z'; c++)
        {
            KeyComboBox.Items.Add(new ComboBoxItem { Content = c.ToString(), Tag = c.ToString() });
        }

        // Add function keys F1-F12
        for (int i = 1; i <= 12; i++)
        {
            KeyComboBox.Items.Add(new ComboBoxItem { Content = $"F{i}", Tag = $"F{i}" });
        }

        // Add number keys 0-9
        for (int i = 0; i <= 9; i++)
        {
            KeyComboBox.Items.Add(new ComboBoxItem { Content = i.ToString(), Tag = $"D{i}" });
        }
    }

    private void LoadCurrentSettings()
    {
        // Set modifier
        foreach (ComboBoxItem item in ModifierComboBox.Items)
        {
            if (item.Tag?.ToString()?.Equals(_settings.HotkeyModifier, StringComparison.OrdinalIgnoreCase) == true)
            {
                ModifierComboBox.SelectedItem = item;
                break;
            }
        }

        if (ModifierComboBox.SelectedItem == null)
            ModifierComboBox.SelectedIndex = 0;

        // Set key
        foreach (ComboBoxItem item in KeyComboBox.Items)
        {
            if (item.Tag?.ToString()?.Equals(_settings.HotkeyKey, StringComparison.OrdinalIgnoreCase) == true)
            {
                KeyComboBox.SelectedItem = item;
                break;
            }
        }

        if (KeyComboBox.SelectedItem == null)
            KeyComboBox.SelectedIndex = 3; // D

        // Debug mode
        DebugModeCheckBox.IsChecked = _settings.DebugMode;

        UpdateHotkeyDisplay();
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateHotkeyDisplay();
    }

    private void UpdateHotkeyDisplay()
    {
        var modifier = (ModifierComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Ctrl";
        var key = (KeyComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "D";
        CurrentHotkeyText.Text = $"{modifier}+{key}";
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var modifierItem = ModifierComboBox.SelectedItem as ComboBoxItem;
        var keyItem = KeyComboBox.SelectedItem as ComboBoxItem;

        if (modifierItem == null || keyItem == null)
        {
            MessageBox.Show("Please select both a modifier and a key.", "Invalid Selection",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _settings.HotkeyModifier = modifierItem.Tag?.ToString() ?? "Control";
        _settings.HotkeyKey = keyItem.Tag?.ToString() ?? "D";
        _settings.DebugMode = DebugModeCheckBox.IsChecked ?? false;

        _settingsManager.SaveSettings(_settings);

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
