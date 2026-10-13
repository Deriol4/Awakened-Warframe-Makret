using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using AwakenedWarframeMarket.Services;

namespace AwakenedWarframeMarket.Models;

/// <summary>Minimal INotifyPropertyChanged base for the overlay's list items.</summary>
public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value))
            return false;
        field = value;
        Raise(name);
        return true;
    }

    protected void Raise(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>One seller in the overlay table.</summary>
public class OrderRow : Observable
{
    private bool _isSelected;
    private bool _isCopied;

    public OrderRow(MarketOrder order, MarketItem item, bool isOdd)
    {
        Order = order;
        IsOdd = isOdd;
        ShowDetails = item.HasDetails;
        DetailWidth = DetailColumnWidth(item);
        DetailText = BuildDetailText(order, item);
        IsMaxRank = item.HasRanks && order.Rank == item.MaxRank;
    }

    /// <summary>Width of the details column: narrow for a rank alone, wider for refinements and the like.</summary>
    public static double DetailColumnWidth(MarketItem item) =>
        item.HasRanks && !item.HasSubtypes && !item.HasCharges && !item.HasStars ? 44 : 84;

    // "5/10", "Radiant", "3 ch", "2+1★", joined with " · " when an item has several
    private static string BuildDetailText(MarketOrder order, MarketItem item)
    {
        var parts = new List<string>();
        if (item.HasSubtypes) parts.Add(string.IsNullOrEmpty(order.Subtype) ? "-" : L.SubtypeName(order.Subtype) ?? MarketItem.SubtypeLabel(order.Subtype));
        if (item.HasRanks) parts.Add(order.Rank != null ? $"{order.Rank}/{item.MaxRank}" : "-");
        if (item.HasCharges) parts.Add(order.Charges != null ? $"{order.Charges} {L.ChargesShort}" : "-");
        if (item.HasStars) parts.Add($"{order.AmberStars ?? 0}+{order.CyanStars ?? 0}★");
        return parts.Count == 0 ? "-" : string.Join(" · ", parts);
    }

    public MarketOrder Order { get; }

    public string Price => $"{Order.Platinum}p";
    public string Quantity => Order.Quantity.ToString();
    public string User => Order.User?.IngameName ?? "";
    public string Status => Order.User?.Status ?? "offline";
    public string StatusLabel => Status switch { "ingame" => L.InGame, "online" => L.Online, _ => L.Offline };
    public bool IsOffline => Status != "ingame" && Status != "online";
    public string Listed => FormatAgo(Order.CreatedAt);

    public bool ShowDetails { get; }
    public double DetailWidth { get; }
    public string DetailText { get; }
    public bool IsMaxRank { get; }
    public bool IsOdd { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    public bool IsCopied
    {
        get => _isCopied;
        set
        {
            if (Set(ref _isCopied, value))
                Raise(nameof(CopyLabel));
        }
    }

    public string CopyLabel => IsCopied ? L.Copied : L.Buy;
    public string CopyTooltip => L.CopyWhisperTo(User);

    public static string FormatAgo(DateTimeOffset time)
    {
        return L.Ago(DateTimeOffset.UtcNow - time);
    }
}

/// <summary>A circle in the set switcher: the whole set or one of its parts.</summary>
public class SetPart : Observable
{
    private bool _isSelected;
    private bool _imageFailed;

    public SetPart(MarketItem item, MarketItem? setRoot)
    {
        Item = item;
        IsSetRoot = item.SetRoot == true;
        ShortLabel = MakeShortLabel(item, setRoot);
        QuantityText = item.QuantityInSet is > 1 ? $"×{item.QuantityInSet}" : "×1";
        HasQuantity = !IsSetRoot;
    }

    public MarketItem Item { get; }
    public string Name => Item.DisplayName;

    // The set shows its own picture. Parts share that same picture on warframe.market, so for them only
    // the part's type icon (blueprint, neuroptics, ...) is used; without one the text label is shown.
    public string? ImageUrl => IsSetRoot ? Item.ThumbUrl : Item.SubIconUrl;
    public bool HasImage => ImageUrl != null && !_imageFailed;

    public void MarkImageFailed()
    {
        if (_imageFailed)
            return;
        _imageFailed = true;
        Raise(nameof(HasImage));
    }
    public bool IsSetRoot { get; }
    public string ShortLabel { get; }
    public string QuantityText { get; }
    public bool HasQuantity { get; }
    public double Size => IsSetRoot ? 52 : 40;

    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    // "Volt Prime Neuroptics Blueprint" in set "Volt Prime Set" -> "NEU"; "Volt Prime Blueprint" -> "BP"
    private static string MakeShortLabel(MarketItem item, MarketItem? setRoot)
    {
        if (item.SetRoot == true)
            return "SET";

        var name = item.Name;
        var baseName = setRoot?.Name;
        if (!string.IsNullOrEmpty(baseName) && baseName.EndsWith(" Set", StringComparison.OrdinalIgnoreCase))
            baseName = baseName[..^4];
        if (!string.IsNullOrEmpty(baseName) && name.StartsWith(baseName, StringComparison.OrdinalIgnoreCase))
            name = name[baseName.Length..];

        var word = name
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(w => !w.Equals("Blueprint", StringComparison.OrdinalIgnoreCase));

        if (word == null)
            return "BP";
        return word.Length <= 3 ? word.ToUpperInvariant() : word[..3].ToUpperInvariant();
    }
}

/// <summary>A row in the "Closest items" list of the manual search.</summary>
public class Suggestion : Observable
{
    private bool _isSelected;

    public Suggestion(MarketItem item) => Item = item;

    public MarketItem Item { get; }
    public string Name => Item.DisplayName;

    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }
}
