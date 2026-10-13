using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AwakenedWarframeMarket.Models;
using Newtonsoft.Json;

namespace AwakenedWarframeMarket.Services;

/// <summary>Result of looking up OCR text in the market catalogue.</summary>
/// <param name="Method">"exact", "fuzzy", "partial" or "words": how the item was found.</param>
/// <param name="Text">The text that matched.</param>
public record ItemMatch(MarketItem Item, string Method, string Text);

public class WarframeMarketAPI
{
    private const string BaseUrl = "https://api.warframe.market/v2";
    public const string SiteUrl = "https://warframe.market";

    private sealed record KeyedItem(MarketItem Item, string Key)
    {
        public string[] Words { get; } = Key.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        public string Compact { get; } = Key.Replace(" ", "");
    }

    private readonly HttpClient _client;
    private List<KeyedItem>? _itemsCache;
    private DateTime _cacheTime;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromHours(1);

    // One catalogue download at a time; after a failure wait a bit before hitting the site again
    private Task<List<KeyedItem>?>? _catalogueLoad;
    private DateTime _lastCatalogueFailure = DateTime.MinValue;
    private static readonly TimeSpan FailureBackoff = TimeSpan.FromSeconds(30);

    public WarframeMarketAPI()
    {
        _client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        var version = typeof(WarframeMarketAPI).Assembly.GetName().Version?.ToString(3) ?? "0.0.1";
        _client.DefaultRequestHeaders.UserAgent.ParseAdd($"AwakenedWarframeMarket/{version}");
        _client.DefaultRequestHeaders.Add("Accept", "application/json");
        // Platform is a required header on the v2 order endpoints
        _client.DefaultRequestHeaders.Add("Platform", "pc");
        // With "ru" the catalogue and sets carry both the English and the Russian names
        _client.DefaultRequestHeaders.Add("Language", "ru");
    }

    public static string ItemPageUrl(string slug) => $"{SiteUrl}/items/{slug}";

    /// <summary>Download the item catalogue in advance so the first price check doesn't wait for it.</summary>
    public Task PreloadAsync() => GetAllItemsAsync();

    /// <summary>False when the item list could not be downloaded (no connection, site down).</summary>
    public bool CatalogueLoaded => _itemsCache is { Count: > 0 };

    // ---- Catalogue lookup ----------------------------------------------------------

    /// <summary>
    /// Picks the item among everything OCR read around the cursor. Each candidate is looked up in the
    /// catalogue; of those that are real items, the one that matches best and sits most like the hovered
    /// item's label wins (see <see cref="CandidateRanking"/>). Null when nothing matches an item.
    /// </summary>
    public async Task<ItemMatch?> FindBestMatchAsync(IReadOnlyList<NameCandidate> candidates)
    {
        var items = await GetAllItemsAsync();
        if (items == null || items.Count == 0 || candidates.Count == 0)
            return null;

        // Thousands of edit-distance comparisons: keep them off the UI thread
        return await Task.Run(() => PickBest(items, CandidateRanking.MarkLabelsOfCellAbove(candidates)));
    }

    // Key: the catalogue name (English or Russian, normalized) the text matched
    private sealed record Scored(ItemMatch Match, double Quality, string Key);

    private static ItemMatch? PickBest(List<KeyedItem> items, IReadOnlyList<NameCandidate> candidates)
    {
        var cache = new Dictionary<(string, bool), Scored?>();
        Scored? Lookup(NameCandidate c)
        {
            var key = (c.Text, c.IsPartOfLine);
            if (!cache.TryGetValue(key, out var result))
                cache[key] = result = Match(items, c.Text, allowLoose: !c.IsPartOfLine);
            return result;
        }

        // Items that whole lines read as. A part of a line that names a shorter item contained in one of
        // these ("Intensify" in a line reading "hon Intensify" = Archon Intensify) loses to the longer name.
        var wholeLineKeys = candidates
            .Where(c => !c.IsPartOfLine)
            .Select(Lookup)
            .Where(m => m != null)
            .Select(m => m!.Key)
            .ToList();

        var prefixes = VariantPrefixes(items);
        double maxHeight = candidates.Max(c => c.Height);
        Scored? best = null;
        NameCandidate? bestCandidate = null;
        double bestScore = double.MaxValue;
        foreach (var c in candidates)
        {
            var m = Lookup(c);
            if (m == null)
                continue;

            if (c.IsPartOfLine)
            {
                var key = m.Key;
                if (wholeLineKeys.Any(k => k != key && k.Contains(key, StringComparison.Ordinal)))
                    continue;

                // "Shred" read out of "Primed Shred": the hovered mod is a variant that isn't on the market
                // (untradeable ones like Primed Shred or Umbral Intensify), not the base mod
                if (c.WordBefore != null && IsVariantPrefix(c.WordBefore, prefixes))
                    continue;
            }

            double score = m.Quality + CandidateRanking.Penalty(c, maxHeight);
            if (score < bestScore)
            {
                bestScore = score;
                best = m;
                bestCandidate = c;
            }
        }

        if (best == null || bestCandidate == null)
            return null;

        // The winner is off to the side of the cursor (a neighbouring card). That's only believable when
        // it was read cleanly and nothing in the cursor's own column looks like a name. Otherwise the
        // hovered item is most likely one that isn't sold on warframe.market (an untradeable mod), and
        // showing the neighbour's prices would be wrong.
        // The winner is a label above the cursor with nothing under it (the cell above in an inventory
        // grid), while the hovered cell's own label below the cursor was read but matches nothing:
        // better to show the search with what was read than another item's prices
        if (bestCandidate.LabelOfCellAbove && candidates.Any(c =>
                !c.IsPartOfLine && !c.Above && c.Dx <= 0.5 * c.Height && c.Text.Any(char.IsLetter) && Lookup(c) == null))
            return null;

        bool offColumn = bestCandidate.Dx > 1.5 * bestCandidate.Height;
        if (offColumn)
        {
            if (best.Quality >= 1.5)
                return null;
            bool unmatchedLabelUnderCursor = candidates.Any(c =>
                !c.IsPartOfLine && c.Dx <= 0.5 * c.Height && c.Height >= 0.95 * bestCandidate.Height &&
                CandidateRanking.LooksLikeName(c.Text) && Lookup(c) == null);
            if (unmatchedLabelUnderCursor)
                return null;
        }
        return best.Match;
    }

    // Words that make a different item out of another one: "Primed Shred", "Umbral Intensify", "Archon Vitality"
    private static readonly string[] KnownVariantPrefixes =
        { "primed", "umbral", "archon", "galvanized", "flawed", "amalgam", "sacrificial", "spectral", "precision", "necramech" };

    private static HashSet<string> _variantPrefixes = new();

    /// <summary>The known prefixes plus every first word that turns one catalogue item into another.</summary>
    private static HashSet<string> VariantPrefixes(List<KeyedItem> items)
    {
        lock (KnownVariantPrefixes)
        {
            if (_variantPrefixItems == items)
                return _variantPrefixes;

            var keys = items.Select(x => x.Key).ToHashSet();
            var result = new HashSet<string>(KnownVariantPrefixes);
            foreach (var x in items)
            {
                if (x.Words.Length >= 2 && keys.Contains(string.Join(' ', x.Words.Skip(1))))
                    result.Add(x.Words[0]);
            }
            _variantPrefixItems = items;
            _variantPrefixes = result;
            return result;
        }
    }

    private static List<KeyedItem>? _variantPrefixItems;

    // Read with slips ("Pr'imed", "Prlmed") or with its start hidden under the cursor ("bral" for "umbral")
    private static bool IsVariantPrefix(string word, HashSet<string> prefixes)
    {
        word = new string(word.Where(char.IsLetter).ToArray()).ToLowerInvariant();
        if (word.Length == 0)
            return false;
        if (prefixes.Contains(word))
            return true;
        if (word.Length >= 3 && prefixes.Any(p => p.EndsWith(word, StringComparison.Ordinal)))
            return true;
        return word.Length >= 5 && prefixes.Any(p => Math.Abs(p.Length - word.Length) <= 1 && LevenshteinDistance(word, p) <= 1);
    }

    /// <summary>
    /// Looks up one piece of text. Quality: 0 for an exact name, more for each guess the match needed.
    /// Order: exact name -> closest by edit distance -> a name that contains the text (unique) -> a name whose
    /// words the text's words begin or end ("hon Intensify" -> Archon Intensify, letters hidden by the cursor).
    /// Returns null instead of guessing when two items fit equally well. The loose steps are skipped for
    /// parts of a line, which are often single words of a mod's description ("Strength", "Mercy").
    /// </summary>
    public static int MinFuzzyMargin = 2;

    private static Scored? Match(List<KeyedItem> items, string text, bool allowLoose)
    {
        var query = Normalize(text);
        if (query.Length == 0)
            return null;

        // 1. Exact (ignoring case and punctuation)
        var exact = items.FirstOrDefault(x => x.Key == query);
        if (exact != null)
            return new Scored(new ItemMatch(exact.Item, "exact", text), 0, exact.Key);

        // 2. Closest by edit distance: a few OCR slips, but not enough to turn one item into another.
        //    Short names get a stricter limit: "Rage" vs "Rift" is two edits but a different mod.
        int maxDistance = query.Length switch
        {
            <= 4 => 0,
            <= 8 => 1,
            _ => Math.Max(2, query.Length / 5)
        };
        // The runner-up is looked for a little beyond the limit too: an item just outside it is still a rival
        int window = maxDistance + MinFuzzyMargin - 1;
        var ranked = items
            .Where(x => Math.Abs(x.Key.Length - query.Length) <= window)
            .Select(x => new { x.Item, x.Key, Distance = LevenshteinDistance(query, x.Key) })
            .Where(x => x.Distance <= window)
            .OrderBy(x => x.Distance)
            .ThenBy(x => x.Item.Name.Length)
            .Take(2)
            .ToList();
        if (ranked.Count > 0 && ranked[0].Distance > maxDistance)
            ranked.Clear();

        // Another item almost as close: don't guess by distance. Prime parts differ in one short word
        // ("Чертёж: Зефир Прайм: Нейрооптика" / "Чертёж: Эш Прайм: Нейрооптика"); when that word is
        // misread, the shortest name wins by a letter or two, which says nothing
        if (ranked.Count == 2 && ranked[1].Distance - ranked[0].Distance < MinFuzzyMargin && ranked[0].Item.Slug != ranked[1].Item.Slug)
            return allowLoose ? MatchWords(items, query, text) : null;
        if (ranked.Count > 0)
            return new Scored(new ItemMatch(ranked[0].Item, "fuzzy", text), 0.5 + ranked[0].Distance, ranked[0].Key);

        if (!allowLoose)
            return null;

        // 3. Part of a name was lost: accept a containing name if it's the only one and the text is most of it
        if (query.Length >= 6)
        {
            var containing = items.Where(x => x.Key.Contains(query, StringComparison.Ordinal)).Take(2).ToList();
            if (containing.Count == 1 && query.Length * 2 >= containing[0].Key.Length)
                return new Scored(new ItemMatch(containing[0].Item, "partial", text),
                    1 + 3 * (1 - (double)query.Length / containing[0].Key.Length), containing[0].Key);
        }

        // 4. Word by word
        return MatchWords(items, query, text);
    }

    private static Scored? MatchWords(List<KeyedItem> items, string query, string text)
    {
        var queryWords = query.Split(' ').Where(w => w.Length >= 3 || w is "of" or "the").ToList();
        if (queryWords.Count < 2)
            return null;

        var found = new List<(double Coverage, KeyedItem Item)>();
        foreach (var item in items)
        {
            var words = item.Words;
            if (words.Length < queryWords.Count)
                continue;

            // The text's words must appear in the same order among the item's words
            int i = 0;
            double covered = 0;
            bool all = true;
            foreach (var q in queryWords)
            {
                while (i < words.Length && WordFit(q, words[i]) == 0)
                    i++;
                if (i == words.Length)
                {
                    all = false;
                    break;
                }
                covered += WordFit(q, words[i]) * words[i].Length;
                i++;
            }
            if (!all)
                continue;

            // Every real word of the name must show up at least in part: "Systems Blueprint" alone
            // is not "Itzal Systems Blueprint", it's the tail of some other label
            if (!words.All(w => w is "of" or "the" or "и" || queryWords.Any(q => WordFit(q, w) > 0)))
                continue;

            double coverage = covered / words.Sum(w => w.Length);
            if (coverage >= 0.6)
                found.Add((coverage, item));
        }

        if (found.Count == 0)
            return null;
        found.Sort((a, b) => b.Coverage.CompareTo(a.Coverage));
        if (found.Count > 1 && found[1].Coverage >= found[0].Coverage - 0.05)
            return null;
        return new Scored(new ItemMatch(found[0].Item.Item, "words", text), 1 + 3 * (1 - found[0].Coverage), found[0].Item.Key);
    }

    // How much of an item's word a read word accounts for: all of it, the visible part of it, or nothing
    private static double WordFit(string read, string word)
    {
        if (read == word)
            return 1;
        if (read.Length >= 3 && (word.StartsWith(read, StringComparison.Ordinal) || word.EndsWith(read, StringComparison.Ordinal)))
            return (double)read.Length / word.Length;
        if (read.Length >= 5 && word.Length >= 5 && LevenshteinDistance(read, word) <= 1)
            return 0.9;
        return 0;
    }

    /// <summary>
    /// Items for the manual search box: names that start with or contain the text come first (also
    /// ignoring spaces, and word by word in any order), then names that are close to it (typo-tolerant, also against the start of longer names,
    /// so "hildryn prlme neuropt" still finds "Hildryn Prime Neuroptics Blueprint").
    /// </summary>
    public async Task<List<MarketItem>> SuggestAsync(string text, int count = 6)
    {
        var items = await GetAllItemsAsync();
        var query = Normalize(text ?? "");
        if (items == null || query.Length == 0)
            return new List<MarketItem>();

        return await Task.Run(() => items
            .Select(x => new { x.Item, Score = SuggestScore(query, x) })
            .Where(x => x.Score < 1000)
            .OrderBy(x => x.Score)
            .ThenBy(x => x.Item.Name.Length)
            .ThenBy(x => x.Item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Item)
            .DistinctBy(i => i.Slug) // an item is listed under its English and its Russian name
            .Take(count)
            .ToList());
    }

    private static double SuggestScore(string query, KeyedItem item)
    {
        var key = item.Key;
        if (key == query) return -300;
        if (key.StartsWith(query, StringComparison.Ordinal)) return -200 + (key.Length - query.Length) * 0.01;
        int idx = key.IndexOf(query, StringComparison.Ordinal);
        if (idx >= 0) return -100 + idx * 0.1 + (key.Length - query.Length) * 0.01;

        // Spaces don't matter: "hotshot" finds "Arcane Hot Shot"
        var compact = query.Replace(" ", "");
        if (compact.Length >= 3)
        {
            idx = item.Compact.IndexOf(compact, StringComparison.Ordinal);
            if (idx >= 0) return -95 + idx * 0.1 + (key.Length - query.Length) * 0.01;
        }

        // Words in any order, each the start of a word in the name: "prime volt neuro"
        var queryWords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (queryWords.Length > 1 && queryWords.All(q => item.Words.Any(w => w.StartsWith(q, StringComparison.Ordinal))))
            return -90 + (key.Length - query.Length) * 0.01;

        // Typo-tolerant: compare with the whole name and with its beginning (same length as the query)
        int limit = Math.Max(2, query.Length / 4);
        int whole = Math.Abs(key.Length - query.Length) <= limit ? LevenshteinDistance(query, key) : int.MaxValue;
        int prefix = key.Length > query.Length ? LevenshteinDistance(query, key.Substring(0, query.Length)) : int.MaxValue;
        int best = Math.Min(whole, prefix);
        return best <= limit ? best + (best == prefix ? 0.5 : 0) : 1000;
    }

    private Task<List<KeyedItem>?> GetAllItemsAsync()
    {
        if (_itemsCache != null && DateTime.Now - _cacheTime < _cacheExpiry)
            return Task.FromResult<List<KeyedItem>?>(_itemsCache);

        // Recently failed: answer with what we have instead of retrying on every keystroke
        if (DateTime.Now - _lastCatalogueFailure < FailureBackoff)
            return Task.FromResult(_itemsCache);

        // Callers arriving while a download is running share it (all on the UI thread, so no lock needed)
        if (_catalogueLoad == null || _catalogueLoad.IsCompleted)
            _catalogueLoad = LoadCatalogueAsync();
        return _catalogueLoad;
    }

    private async Task<List<KeyedItem>?> LoadCatalogueAsync()
    {
        try
        {
            var response = await _client.GetStringAsync($"{BaseUrl}/items");

            // The catalogue is a few MB of JSON: parse it in the background, not on the UI thread
            var parsed = await Task.Run(() =>
                JsonConvert.DeserializeObject<ApiResponse<List<MarketItem>>>(response)?.Data?
                    .Where(i => i.Name.Length > 0)
                    .SelectMany(KeysOf)
                    .ToList());

            if (parsed is { Count: > 0 })
            {
                _itemsCache = parsed;
                _cacheTime = DateTime.Now;
            }
            else
            {
                _lastCatalogueFailure = DateTime.Now;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching items list: {ex.Message}");
            _lastCatalogueFailure = DateTime.Now;
        }

        return _itemsCache; // stale data is better than none
    }

    // Every name an item can be found by: English, and Russian when the catalogue has it
    private static IEnumerable<KeyedItem> KeysOf(MarketItem item)
    {
        var english = Normalize(item.Name);
        yield return new KeyedItem(item, english);
        if (item.RussianName is { } russian && Normalize(russian) is var key && key.Length > 0 && key != english)
        {
            yield return new KeyedItem(item, key);

            // The site writes "Зефир Прайм: Нейрооптика (Чертеж)", the game "Чертёж: Зефир Прайм: Нейрооптика"
            const string blueprint = "(Чертеж)";
            if (russian.EndsWith(blueprint, StringComparison.OrdinalIgnoreCase))
                yield return new KeyedItem(item, Normalize("Чертеж " + russian[..^blueprint.Length]));
        }
    }

    // Latin letters that look like Cyrillic ones: Russian names keep a few Latin letters ("Реликвия Акси A1"),
    // and Russian OCR reads them as Cyrillic, or the other way round
    private const string LatinLookalikes = "aceopxykmhtb";
    private const string CyrillicLookalikes = "асеорхукмнтв";

    // "Ash Prime  Neuroptics-Blueprint" -> "ash prime neuroptics blueprint"; "Чертёж" -> "чертеж"
    private static string Normalize(string text)
    {
        text = Regex.Replace(text.ToLowerInvariant().Replace('ё', 'е'), @"[^\p{L}\p{N}]+", " ").Trim();
        if (!text.Any(ch => ch is >= 'а' and <= 'я'))
            return text;
        var chars = text.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            int k = LatinLookalikes.IndexOf(chars[i]);
            if (k >= 0)
                chars[i] = CyrillicLookalikes[k];
        }
        return new string(chars);
    }

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
