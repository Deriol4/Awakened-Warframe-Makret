namespace AwakenedWarframeMarket.Models;

public class Settings
{
    public string HotkeyModifier { get; set; } = "Control";
    public string HotkeyKey { get; set; } = "D";
    // The hotkey is only taken while Warframe (or this app) is in front, so other apps keep Ctrl+D
    public bool OnlyInWarframe { get; set; } = true;
}
