namespace AwakenedWarframeMarket.Models;

public class Settings
{
    // Bumped when defaults change; SettingsManager migrates older files
    public const int CurrentVersion = 2;

    public int Version { get; set; }

    public string HotkeyModifier { get; set; } = "Alt";
    public string HotkeyKey { get; set; } = "X";

    // The hotkey is only taken while Warframe (or this app) is in front, so other apps keep the keys
    public bool OnlyInWarframe { get; set; } = true;
}
