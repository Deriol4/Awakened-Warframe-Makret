using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AwakenedWarframeMarket.Models;
using Newtonsoft.Json;

namespace AwakenedWarframeMarket.Services;

public class WarframeMarketAPI
{
    private readonly HttpClient _client;
    private List<MarketItem>? _itemsCache;
    private DateTime _cacheTime;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromHours(1);

    public WarframeMarketAPI()
    {
        _client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        _client.DefaultRequestHeaders.Add("Accept", "application/json");
        _client.DefaultRequestHeaders.Add("Platform", "pc");
        _client.DefaultRequestHeaders.Add("Language", "en");
    }

    public async Task<string?> FindClosestItemNameAsync(string itemName)
    {
        var items = await GetAllItemsAsync();
        if (items == null || items.Count == 0)
            return null;

        // Try exact match first (case-insensitive)
        var exactMatch = items.FirstOrDefault(i =>
            i.ItemName.Equals(itemName, StringComparison.OrdinalIgnoreCase));

        if (exactMatch != null)
            return exactMatch.UrlName;

        // Try contains match
        var containsMatch = items.FirstOrDefault(i =>
            i.ItemName.Contains(itemName, StringComparison.OrdinalIgnoreCase) ||
            itemName.Contains(i.ItemName, StringComparison.OrdinalIgnoreCase));

        if (containsMatch != null)
            return containsMatch.UrlName;

        // Try fuzzy match using Levenshtein distance
        var bestMatch = items
            .Select(i => new { Item = i, Distance = LevenshteinDistance(itemName.ToLower(), i.ItemName.ToLower()) })
            .Where(x => x.Distance <= Math.Max(3, itemName.Length / 3)) // Allow up to 1/3 of chars to be wrong
            .OrderBy(x => x.Distance)
            .FirstOrDefault();

        return bestMatch?.Item.UrlName;
    }

    private async Task<List<MarketItem>?> GetAllItemsAsync()
    {
        if (_itemsCache != null && DateTime.Now - _cacheTime < _cacheExpiry)
            return _itemsCache;

        try
        {
            var response = await _client.GetStringAsync("https://api.warframe.market/v1/items");
            var data = JsonConvert.DeserializeObject<ItemsListResponse>(response);

            _itemsCache = data?.Payload?.Items;
            _cacheTime = DateTime.Now;

            return _itemsCache;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching items list: {ex.Message}");
            return _itemsCache; // Return cached data if available
        }
    }

    public async Task<List<PriceData>> GetItemPricesAsync(string urlName)
    {
        try
        {
            var url = $"https://api.warframe.market/v1/items/{urlName}/orders";
            var response = await _client.GetStringAsync(url);
            var data = JsonConvert.DeserializeObject<MarketResponse>(response);

            if (data?.Payload?.Orders == null)
                return new List<PriceData>();

            var prices = data.Payload.Orders
                .Where(o => o.OrderType == "sell" && o.User?.Status == "ingame")
                .OrderBy(o => o.Platinum)
                .Take(20)
                .Select(o => new PriceData
                {
                    Price = o.Platinum,
                    Rank = o.ModRank?.ToString() ?? "-",
                    Quality = "-", // API doesn't provide quality directly
                    ListedTime = FormatTimeAgo(o.LastUpdate),
                    UserName = o.User?.InGameName ?? "",
                    UserStatus = o.User?.Status ?? ""
                })
                .ToList();

            // If no in-game users, include online users
            if (prices.Count < 5)
            {
                var onlineOrders = data.Payload.Orders
                    .Where(o => o.OrderType == "sell" && o.User?.Status == "online")
                    .OrderBy(o => o.Platinum)
                    .Take(20 - prices.Count)
                    .Select(o => new PriceData
                    {
                        Price = o.Platinum,
                        Rank = o.ModRank?.ToString() ?? "-",
                        Quality = "-",
                        ListedTime = FormatTimeAgo(o.LastUpdate),
                        UserName = o.User?.InGameName ?? "",
                        UserStatus = o.User?.Status ?? ""
                    });

                prices.AddRange(onlineOrders);
            }

            return prices.OrderBy(p => p.Price).Take(20).ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching prices: {ex.Message}");
            throw;
        }
    }

    private string FormatTimeAgo(DateTime time)
    {
        var diff = DateTime.UtcNow - time;

        if (diff.TotalMinutes < 1) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return time.ToString("MMM dd");
    }

    private static int LevenshteinDistance(string s1, string s2)
    {
        int[,] d = new int[s1.Length + 1, s2.Length + 1];

        for (int i = 0; i <= s1.Length; i++)
            d[i, 0] = i;

        for (int j = 0; j <= s2.Length; j++)
            d[0, j] = j;

        for (int j = 1; j <= s2.Length; j++)
        {
            for (int i = 1; i <= s1.Length; i++)
            {
                int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[s1.Length, s2.Length];
    }
}
