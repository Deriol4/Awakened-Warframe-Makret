using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AwakenedWarframeMarket.Models;

// warframe.market API v2 wraps every response as { "apiVersion", "data", "error" }.
// v1 used { "payload": ... } with snake_case fields; v2 uses "data" with camelCase.
public class ApiResponse<T>
{
    [JsonProperty("apiVersion")]
    public string? ApiVersion { get; set; }

    [JsonProperty("data")]
    public T? Data { get; set; }

    [JsonProperty("error")]
    public JToken? Error { get; set; }
}

public class MarketOrder
{
    [JsonProperty("id")]
    public string Id { get; set; } = "";

    // "buy" or "sell" (v1: order_type)
    [JsonProperty("type")]
    public string Type { get; set; } = "";

    [JsonProperty("platinum")]
    public int Platinum { get; set; }

    [JsonProperty("quantity")]
    public int Quantity { get; set; }

    // Mod/arcane rank (v1: mod_rank). Null for items that have no rank.
    [JsonProperty("rank")]
    public int? Rank { get; set; }

    [JsonProperty("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonProperty("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonProperty("user")]
    public MarketUser? User { get; set; }
}

public class MarketUser
{
    [JsonProperty("ingameName")]
    public string IngameName { get; set; } = "";

    // invisible | offline | online | ingame
    [JsonProperty("status")]
    public string Status { get; set; } = "";

    [JsonProperty("reputation")]
    public int Reputation { get; set; }
}

public class MarketItem
{
    // Where warframe.market serves item images; i18n icon/thumb paths are relative to it
    public const string AssetsBaseUrl = "https://warframe.market/static/assets/";

    // v1: url_name
    [JsonProperty("slug")]
    public string Slug { get; set; } = "";

    [JsonProperty("id")]
    public string Id { get; set; } = "";

    [JsonProperty("tags")]
    public List<string>? Tags { get; set; }

    // Highest rank for mods/arcanes; null for items without ranks
    [JsonProperty("maxRank")]
    public int? MaxRank { get; set; }

    [JsonProperty("ducats")]
    public int? Ducats { get; set; }

    // True for the "... Set" item itself
    [JsonProperty("setRoot")]
    public bool? SetRoot { get; set; }

    // Ids of all items in the set (root included)
    [JsonProperty("setParts")]
    public List<string>? SetParts { get; set; }

    [JsonProperty("quantityInSet")]
    public int? QuantityInSet { get; set; }

    // Localized texts keyed by language code, e.g. { "en": { "name": "Arcane Blessing" } }
    [JsonProperty("i18n")]
    public Dictionary<string, MarketItemI18N>? I18N { get; set; }

    // Display name (v1: item_name). Prefers English, falls back to any available language.
    [JsonIgnore]
    public string Name => English?.Name is { Length: > 0 } n ? n : I18N?.Values.Select(v => v.Name).FirstOrDefault(x => !string.IsNullOrEmpty(x)) ?? "";

    [JsonIgnore]
    public string? ThumbUrl
    {
        get
        {
            var path = English?.Thumb ?? English?.Icon;
            return string.IsNullOrEmpty(path) ? null : AssetsBaseUrl + path.TrimStart('/');
        }
    }

    // Small type icon of a set part (blueprint, neuroptics, barrel...); parts share the set's main picture
    [JsonIgnore]
    public string? SubIconUrl =>
        string.IsNullOrEmpty(English?.SubIcon) ? null : AssetsBaseUrl + English!.SubIcon!.TrimStart('/');

    [JsonIgnore]
    public bool HasRanks => MaxRank is > 0;

    private MarketItemI18N? English =>
        I18N != null && I18N.TryGetValue("en", out var en) ? en : I18N?.Values.FirstOrDefault();
}

public class MarketItemI18N
{
    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("icon")]
    public string? Icon { get; set; }

    [JsonProperty("thumb")]
    public string? Thumb { get; set; }

    [JsonProperty("subIcon")]
    public string? SubIcon { get; set; }
}

// GET /v2/item/{slug}/set
public class ItemSetData
{
    [JsonProperty("id")]
    public string Id { get; set; } = "";

    [JsonProperty("items")]
    public List<MarketItem>? Items { get; set; }
}
