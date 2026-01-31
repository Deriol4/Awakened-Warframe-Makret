namespace AwakenedWarframeMarket.Models;

public class PriceData
{
    public int Price { get; set; }
    public string PriceDisplay => $"{Price} platinum";
    public string Rank { get; set; } = "-";
    public string Quality { get; set; } = "-";
    public string ListedTime { get; set; } = "-";
    public string UserName { get; set; } = "";
    public string UserStatus { get; set; } = "";
}
