namespace AwakenedWarframeMarket.Models;

public class Settings
{
    // Bumped when defaults change; SettingsManager migrates older files
    public const int CurrentVersion = 2;

    public int Version { get; set; }

    public string HotkeyModifier { get; set; } = "Alt";
    public string HotkeyKey { get; set; } = "X";

    // Opens the overlay straight in the search, to look up items you don't have
    public string SearchHotkeyModifier { get; set; } = "Alt";
    public string SearchHotkeyKey { get; set; } = "F";

    // "en" or "ru"; empty follows the Windows display language
    public string Language { get; set; } = "";

    // The language Warframe shows item names in, for reading them from the screen; empty: same as above
    public string GameLanguage { get; set; } = "";

    // The hotkey is only taken while Warframe (or this app) is in front, so other apps keep the keys
    public bool OnlyInWarframe { get; set; } = true;
}
