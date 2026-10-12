using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AwakenedWarframeMarket.Models;
using Newtonsoft.Json;

namespace AwakenedWarframeMarket.Services;

/// <summary>Result of looking up OCR text in the market catalogue.</summary>
/// <param name="Method">"exact", "fuzzy" or "unique-partial" - how the item was found.</param>
public record ItemMatch(MarketItem Item, string Method);

public class WarframeMarketAPI
{
    private const string BaseUrl = "https://api.warframe.market/v2";
    public const string SiteUrl = "https://warframe.market";

    private sealed record KeyedItem(MarketItem Item, string Key);

    private readonly HttpClient _client;
    private List<KeyedItem>? _itemsCache;
    private DateTime _cacheTime;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromHours(1);

    public WarframeMarketAPI()
    {
        _client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        _client.DefaultRequestHeaders.Add("Accept", "application/json");
        // Platform is a required header on the v2 order endpoints
        _client.DefaultRequestHeaders.Add("Platform", "pc");
        _client.DefaultRequestHeaders.Add("Language", "en");
    }

    public static string ItemPageUrl(string slug) => $"{SiteUrl}/items/{slug}";

    /// <summary>Download the item catalogue in advance so the first price check doesn't wait for it.</summary>
    public Task PreloadAsync() => GetAllItemsAsync();

    /// <summary>False when the item list could not be downloaded (no connection, site down).</summary>
    public bool CatalogueLoaded => _itemsCache is { Count: > 0 };

    // ---- Catalogue lookup ----------------------------------------------------------

    /// <summary>
    /// Finds the catalogue item that best matches the text read by OCR.
    /// Order: exact name -> closest by edit distance -> a name that contains the text, but only if it is unique.
    /// Deliberately returns null instead of guessing when the text is ambiguous (e.g. just "Hildryn Prime"
    /// could be the Set or any of its parts).
    /// </summary>
    public async Task<ItemMatch?> FindBestMatchAsync(string ocrText)
    {
        var items = await GetAllItemsAsync();
        if (items == null || items.Count == 0 || string.IsNullOrWhiteSpace(ocrText))
            return null;

        // Thousands of edit-distance comparisons: keep them off the UI thread
        return await Task.Run(() => FindBestMatch(items, ocrText));
    }

    private static ItemMatch? FindBestMatch(List<KeyedItem> items, string ocrText)
    {
        var query = Normalize(ocrText);
        if (query.Length == 0)
            return null;

        // 1. Exact (ignoring case and punctuation)
        var exact = items.FirstOrDefault(x => x.Key == query);
        if (exact != null)
            return new ItemMatch(exact.Item, "exact");

        // 2. Closest by edit distance. The limit is ~20% of the text length: enough for a few OCR slips,
        //    too small to turn "... Systems Blueprint" into a different item.
        int maxDistance = Math.Max(2, query.Length / 5);
        var ranked = items
            .Where(x => Math.Abs(x.Key.Length - query.Length) <= maxDistance)
            .Select(x => new { x.Item, Distance = LevenshteinDistance(query, x.Key) })
            .OrderBy(x => x.Distance)
            .ThenBy(x => x.Item.Name.Length)
            .Take(1)
            .ToList();

        if (ranked.Count > 0 && ranked[0].Distance <= maxDistance)
        {
            return new ItemMatch(ranked[0].Item, "fuzzy");
        }

        // 3. OCR lost part of a long name: accept a containing name only if exactly one item contains the text
        var containing = items.Where(x => x.Key.Contains(query, StringComparison.Ordinal)).Take(2).ToList();
        if (containing.Count == 1)
            return new ItemMatch(containing[0].Item, "unique-partial");

        return null;
    }

    /// <summary>
    /// Items for the manual search box: names that start with or contain the text come first,
    /// then names that are close to it (typo-tolerant, also against the start of longer names,
    /// so "hildryn prlme neuropt" still finds "Hildryn Prime Neuroptics Blueprint").
    /// </summary>
    public async Task<List<MarketItem>> SuggestAsync(string text, int count = 6)
    {
        var items = await GetAllItemsAsync();
        var query = Normalize(text ?? "");
        if (items == null || query.Length == 0)
            return new List<MarketItem>();

        return await Task.Run(() => items
            .Select(x => new { x.Item, Score = SuggestScore(query, x.Key) })
            .Where(x => x.Score < 1000)
            .OrderBy(x => x.Score)
            .ThenBy(x => x.Item.Name.Length)
            .ThenBy(x => x.Item.Name, StringComparer.OrdinalIgnoreCase)
            .Take(count)
            .Select(x => x.Item)
            .ToList());
    }

    private static double SuggestScore(string query, string key)
    {
        if (key == query) return -300;
        if (key.StartsWith(query, StringComparison.Ordinal)) return -200 + (key.Length - query.Length) * 0.01;
        int idx = key.IndexOf(query, StringComparison.Ordinal);
        if (idx >= 0) return -100 + idx * 0.1 + (key.Length - query.Length) * 0.01;

        // Typo-tolerant: compare with the whole name and with its beginning (same length as the query)
        int limit = Math.Max(2, query.Length / 4);
        int whole = Math.Abs(key.Length - query.Length) <= limit ? LevenshteinDistance(query, key) : int.MaxValue;
        int prefix = key.Length > query.Length ? LevenshteinDistance(query, key.Substring(0, query.Length)) : int.MaxValue;
        int best = Math.Min(whole, prefix);
        return best <= limit ? best + (best == prefix ? 0.5 : 0) : 1000;
    }

    private async Task<List<KeyedItem>?> GetAllItemsAsync()
    {
        if (_itemsCache != null && DateTime.Now - _cacheTime < _cacheExpiry)
            return _itemsCache;

        try
        {
            var response = await _client.GetStringAsync($"{BaseUrl}/items");

            // The catalogue is a few MB of JSON: parse it in the background, not on the UI thread
            var parsed = await Task.Run(() =>
                JsonConvert.DeserializeObject<ApiResponse<List<MarketItem>>>(response)?.Data?
                    .Where(i => i.Name.Length > 0)
                    .Select(i => new KeyedItem(i, Normalize(i.Name)))
                    .ToList());

            if (parsed is { Count: > 0 })
            {
                _itemsCache = parsed;
                _cacheTime = DateTime.Now;
            }

            return _itemsCache;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching items list: {ex.Message}");
            return _itemsCache; // Return cached data if available
        }
    }

    // "Ash Prime  Neuroptics-Blueprint" -> "ash prime neuroptics blueprint"
    private static string Normalize(string text) =>
        Regex.Replace(text.ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ").Trim();

    // ---- Sets and orders -----------------------------------------------------------

    /// <summary>
    /// All items of the set this item belongs to, the "... Set" item first.
    /// For an item that is not part of a set (or on error) returns just the item itself.
    /// </summary>
    public async Task<List<MarketItem>> GetSetItemsAsync(MarketItem item)
    {
        try
        {
            var response = await _client.GetStringAsync($"{BaseUrl}/item/{item.Slug}/set");
            var items = await Task.Run(() =>
                JsonConvert.DeserializeObject<ApiResponse<ItemSetData>>(response)?.Data?.Items?
                    .Where(i => !string.IsNullOrEmpty(i.Slug))
                    .ToList());

            if (items == null || items.Count == 0)
                return new List<MarketItem> { item };

            return items
                .OrderByDescending(i => i.SetRoot == true)
                .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching set for {item.Slug}: {ex.Message}");
            return new List<MarketItem> { item };
        }
    }

    /// <summary>Sell orders for an item, from users seen in the last days (online, in game and offline).</summary>
    public async Task<List<MarketOrder>> GetSellOrdersAsync(string slug)
    {
        var response = await _client.GetStringAsync($"{BaseUrl}/orders/item/{slug}");

        // Popular mods and arcanes have hundreds of orders: parse in the background
        return await Task.Run(() =>
            JsonConvert.DeserializeObject<ApiResponse<List<MarketOrder>>>(response)?.Data?
                .Where(o => o.Type == "sell" && o.User != null)
                .ToList() ?? new List<MarketOrder>());
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
