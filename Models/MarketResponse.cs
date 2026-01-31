using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace AwakenedWarframeMarket.Models;

public class MarketResponse
{
    [JsonProperty("payload")]
    public MarketPayload? Payload { get; set; }
}

public class MarketPayload
{
    [JsonProperty("orders")]
    public List<MarketOrder>? Orders { get; set; }
}

public class MarketOrder
{
    [JsonProperty("order_type")]
    public string OrderType { get; set; } = "";

    [JsonProperty("platinum")]
    public int Platinum { get; set; }

    [JsonProperty("quantity")]
    public int Quantity { get; set; }

    [JsonProperty("mod_rank")]
    public int? ModRank { get; set; }

    [JsonProperty("user")]
    public MarketUser? User { get; set; }

    [JsonProperty("creation_date")]
    public DateTime CreationDate { get; set; }

    [JsonProperty("last_update")]
    public DateTime LastUpdate { get; set; }
}

public class MarketUser
{
    [JsonProperty("ingame_name")]
    public string InGameName { get; set; } = "";

    [JsonProperty("status")]
    public string Status { get; set; } = "";

    [JsonProperty("reputation")]
    public int Reputation { get; set; }
}

public class ItemsListResponse
{
    [JsonProperty("payload")]
    public ItemsPayload? Payload { get; set; }
}

public class ItemsPayload
{
    [JsonProperty("items")]
    public List<MarketItem>? Items { get; set; }
}

public class MarketItem
{
    [JsonProperty("url_name")]
    public string UrlName { get; set; } = "";

    [JsonProperty("item_name")]
    public string ItemName { get; set; } = "";

    [JsonProperty("id")]
    public string Id { get; set; } = "";
}
