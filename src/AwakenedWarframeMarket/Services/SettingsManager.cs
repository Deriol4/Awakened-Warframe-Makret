using System;
using System.IO;
using AwakenedWarframeMarket.Models;
using Newtonsoft.Json;

namespace AwakenedWarframeMarket.Services;

/// <summary>
/// settings.json lives in %APPDATA%\AwakenedWarframeMarket, so the app also works from a read-only
/// folder such as Program Files. A settings.json left next to the exe by older versions is picked up once.
/// </summary>
public class SettingsManager
{
    public static readonly string SettingsFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AwakenedWarframeMarket");

    private readonly string _settingsPath = Path.Combine(SettingsFolder, "settings.json");
    private readonly string _legacyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

    public Settings LoadSettings()
    {
        var path = File.Exists(_settingsPath) ? _settingsPath
            : File.Exists(_legacyPath) ? _legacyPath
            : null;

        Settings settings;
        try
        {
            settings = path != null
                ? JsonConvert.DeserializeObject<Settings>(File.ReadAllText(path)) ?? new Settings()
                : new Settings { Version = Settings.CurrentVersion };
        }
        catch (Exception ex)
        {
            // Corrupted file: start from defaults rather than refusing to run
            System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            settings = new Settings { Version = Settings.CurrentVersion };
        }

        if (Migrate(settings) || path == _legacyPath)
            SaveSettings(settings);

        return settings;
    }

    /// <returns>True if something changed.</returns>
    private static bool Migrate(Settings settings)
    {
        if (settings.Version >= Settings.CurrentVersion)
            return false;

        // v1 defaulted to Ctrl+D, which is crouch + strafe right in Warframe and fired mid-mission
        if (settings.Version < 2 &&
            HotkeyManager.ParseModifier(settings.HotkeyModifier) == System.Windows.Input.ModifierKeys.Control &&
            HotkeyManager.ParseKey(settings.HotkeyKey) == System.Windows.Input.Key.D)
        {
            settings.HotkeyModifier = HotkeyManager.ModifierToSetting(HotkeyManager.DefaultModifiers);
            settings.HotkeyKey = HotkeyManager.DefaultKey.ToString();
        }

        settings.Version = Settings.CurrentVersion;
        return true;
    }

    /// <returns>Null on success, otherwise a message to show the user.</returns>
    public string? SaveSettings(Settings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsFolder);
            var json = JsonConvert.SerializeObject(settings, Formatting.Indented);

            // Write to a temp file first so a crash mid-write can't leave a half-written settings.json
            var tempPath = _settingsPath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _settingsPath, overwrite: true);
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            return $"Couldn't save settings to {SettingsFolder}: {ex.Message}";
        }
    }
}
