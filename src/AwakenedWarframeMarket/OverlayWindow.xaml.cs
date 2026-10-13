using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AwakenedWarframeMarket.Models;
using AwakenedWarframeMarket.Services;

namespace AwakenedWarframeMarket;

/// <summary>
/// The price-check overlay. States: loading, results, manual search (name not read or not matched), error.
/// Keys: Esc close · T open on site · B copy whisper for the selected seller · ↑↓ move selection.
/// </summary>
public partial class OverlayWindow : Window
{
    private readonly WarframeMarketAPI _api;
    private readonly Dictionary<string, List<MarketOrder>> _orderCache = new();

    // Every load bumps the version; results of an older load are dropped (e.g. after switching parts quickly)
    private int _version;
    private int _suggestionVersion;

    private MarketItem? _item;
    private List<MarketOrder> _orders = new();
    private List<SetPart> _parts = new();
    private List<OrderRow> _rows = new();
    private List<Suggestion> _suggestions = new();

    private bool _isClosed;
    private bool _isClosing;         // fading out: don't take focus back
    private string? _pendingSlug;    // set part being loaded; _item is null meanwhile
    private int? _rank;              // null = any rank
    private string? _subtype;        // relic refinement etc.; null = any
    private const string AnySubtype = "*any*";
    private string _filter = "ingame";  // all | online | ingame; "In game" first: those sellers can trade right now
    private string _sort = "price";  // price | price-desc | rank | newest
    private string? _selectedOrderId;
    private string? _copiedOrderId;

    public OverlayWindow(WarframeMarketAPI api)
    {
        _api = api;
        InitializeComponent();
        Opacity = 0;
        ShowPanel(LoadingPanel);

        // Lives only while it has focus: clicking back into the game, Alt+Tab or opening the site closes it
        Deactivated += (_, _) => CloseWithAnimation();
    }

    // ---- Public API used by PriceCheckService ----------------------------------------

    public void ShowLoading(string title, string subtitle)
    {
        if (_isClosed)
            return;
        ShowPanel(LoadingPanel);
        LoadingTitle.Text = title;
        LoadingSubtitle.Text = subtitle;
    }

    public void ShowError(string message)
    {
        _version++;
        if (_isClosed)
            return;
        ShowPanel(ErrorPanel);
        ErrorText.Text = message;
    }

    /// <summary>Let the user type the name when OCR could not read it or no item matched.</summary>
    public void ShowSearch(string ocrText, string title) => ShowSearch(ocrText, title, fromScreen: true);

    /// <summary>The search on its own, opened by the search hotkey: find any item, not only one on screen.</summary>
    public void ShowFreeSearch() => ShowSearch("", L.FindAnItem, fromScreen: false);

    private void ShowSearch(string ocrText, string title, bool fromScreen)
    {
        _version++;
        if (_isClosed)
            return;
        ShowPanel(SearchPanel);
        SearchTitle.Text = title;
        SearchOcrLine.Visibility = fromScreen ? Visibility.Visible : Visibility.Collapsed;
        // Read cleanly but not on the market: most likely an item that can't be traded
        SearchHint.Visibility = fromScreen && !string.IsNullOrWhiteSpace(ocrText) ? Visibility.Visible : Visibility.Collapsed;
        SearchLabel.Margin = new Thickness(0, fromScreen ? 14 : 10, 0, 6);
        SearchPlaceholder.Text = fromScreen ? L.TypeToCorrect : L.TypeAnyPart;
        SearchOcrRun.Text = string.IsNullOrWhiteSpace(ocrText) ? L.Nothing : $"“{ocrText}”";
        SearchBox.Text = ocrText;
        SearchBox.SelectAll();

        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (_isClosed)
                return;
            Activate();
            SearchBox.Focus();
            Keyboard.Focus(SearchBox);
        }));
    }

    /// <summary>Load the item, its set (for the part switcher) and its sellers.</summary>
    public async Task LoadItemAsync(MarketItem item)
    {
        if (_isClosed || _isClosing)
            return;

        int version = ++_version;
        ShowLoading(L.LoadingSellers, item.DisplayName);

        try
        {
            var setTask = _api.GetSetItemsAsync(item);
            var ordersTask = GetOrdersAsync(item.Slug);
            await Task.WhenAll(setTask, ordersTask);
            if (version != _version)
                return;

            var setItems = setTask.Result;
            var current = setItems.FirstOrDefault(i => i.Slug == item.Slug) ?? item;
            BuildParts(setItems, current);
            ShowItem(current, ordersTask.Result);
        }
        catch (Exception ex)
        {
            if (version == _version)
                ShowError(L.CouldntLoadSellersFor(item.DisplayName, FriendlyError.Describe(ex)));
        }
    }

    // ---- Window --------------------------------------------------------------------

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PositionNearCursor();
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
        Activate();
    }

    /// <summary>Open beside the cursor (right side if it fits, else left), so the hovered item stays visible.</summary>
    private void PositionNearCursor()
    {
        var cursor = System.Windows.Forms.Cursor.Position;
        var area = System.Windows.Forms.Screen.FromPoint(cursor).WorkingArea;
        var dpi = VisualTreeHelper.GetDpi(this);

        double left = area.Left / dpi.DpiScaleX, right = area.Right / dpi.DpiScaleX;
        double top = area.Top / dpi.DpiScaleY, bottom = area.Bottom / dpi.DpiScaleY;
        double cx = cursor.X / dpi.DpiScaleX, cy = cursor.Y / dpi.DpiScaleY;
        const double margin = 8, gap = 48;

        Height = Math.Min(720, bottom - top - 2 * margin);

        double x = cx + gap + Width <= right - margin ? cx + gap : cx - gap - Width;
        Left = Math.Max(left + margin, Math.Min(x, right - Width - margin));
        Top = Math.Max(top + margin, Math.Min(cy - Height / 2, bottom - Height - margin));
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (SortPopup.IsOpen)
                SortPopup.IsOpen = false;
            else
                CloseWithAnimation();
            e.Handled = true;
            return;
        }

        // Shortcuts only in the results view, and never while typing
        if (ResultsPanel.Visibility != Visibility.Visible || Keyboard.FocusedElement is TextBox)
            return;

        switch (e.Key)
        {
            case Key.T:
                OpenSite();
                e.Handled = true;
                break;
            case Key.B:
                var selected = _rows.FirstOrDefault(r => r.IsSelected);
                if (selected != null)
                    Copy(selected);
                e.Handled = true;
                break;
            case Key.Down:
            case Key.Up:
                MoveSelection(e.Key == Key.Down ? 1 : -1);
                e.Handled = true;
                break;
        }
    }

    private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => CloseWithAnimation();

    private void CloseWithAnimation()
    {
        if (_isClosed || _isClosing)
            return;

        _isClosing = true;
        _version++;
        var fadeOut = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(120));
        fadeOut.Completed += (_, _) =>
        {
            if (!_isClosed)
                Close();
        };
        BeginAnimation(OpacityProperty, fadeOut);
    }

    protected override void OnClosed(EventArgs e)
    {
        _isClosed = true;
        _version++;

        // Stop endless animations, otherwise the closed window keeps the render loop busy and stays in memory
        LoadingBarShift.BeginAnimation(TranslateTransform.XProperty, null);
        BeginAnimation(OpacityProperty, null);
        base.OnClosed(e);
    }

    private void PartImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
    {
        // Icon couldn't be downloaded: fall back to the text label (SET, NEU, ...)
        if ((sender as FrameworkElement)?.DataContext is SetPart part)
            part.MarkImageFailed();
    }

    private void ShowPanel(FrameworkElement panel)
    {
        foreach (var p in new FrameworkElement[] { LoadingPanel, ErrorPanel, SearchPanel, ResultsPanel })
            p.Visibility = p == panel ? Visibility.Visible : Visibility.Collapsed;

        if (panel == LoadingPanel)
        {
            var slide = new DoubleAnimation(-140, 420, TimeSpan.FromSeconds(1.1)) { RepeatBehavior = RepeatBehavior.Forever };
            LoadingBarShift.BeginAnimation(TranslateTransform.XProperty, slide);
        }
        else
        {
            LoadingBarShift.BeginAnimation(TranslateTransform.XProperty, null);
        }
    }

    // ---- Results -------------------------------------------------------------------

    private async Task<List<MarketOrder>> GetOrdersAsync(string slug)
    {
        if (_orderCache.TryGetValue(slug, out var cached))
            return cached;

        var orders = await _api.GetSellOrdersAsync(slug);
        _orderCache[slug] = orders;
        return orders;
    }

    private void BuildParts(List<MarketItem> setItems, MarketItem current)
    {
        var root = setItems.FirstOrDefault(i => i.SetRoot == true);
        _parts = setItems.Count > 1 ? setItems.Select(i => new SetPart(i, root)).ToList() : new List<SetPart>();
        PartsList.ItemsSource = _parts;
        PartsList.Visibility = _parts.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        MarkSelectedPart(current.Slug);
    }

    private void MarkSelectedPart(string slug)
    {
        foreach (var p in _parts)
            p.IsSelected = p.Item.Slug == slug;
    }

    private async void Part_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not SetPart part || part.Item.Slug == (_pendingSlug ?? _item?.Slug))
            return;

        int version = ++_version;
        MarkSelectedPart(part.Item.Slug);
        SetHeader(part.Item);

        // Until the new orders arrive, show nothing of the previous item (B, T and filters do nothing meanwhile)
        _pendingSlug = part.Item.Slug;
        _item = null;
        _orders = new List<MarketOrder>();
        _rows = new List<OrderRow>();
        RowsList.ItemsSource = null;
        ClearStats();
        ShowListMessage(L.LoadingSellersDots);

        try
        {
            var orders = await GetOrdersAsync(part.Item.Slug);
            if (version != _version)
                return;
            ShowItem(part.Item, orders);
        }
        catch (Exception ex)
        {
            if (version == _version)
                ShowListMessage(L.CouldntLoadSellers(FriendlyError.Describe(ex)));
        }
    }

    private void ShowItem(MarketItem item, List<MarketOrder> orders)
    {
        if (_isClosed || _isClosing)
            return;

        _pendingSlug = null;
        _item = item;
        _orders = orders;
        _rank = null;
        _subtype = item.HasSubtypes ? item.Subtypes![0] : null;
        _selectedOrderId = null;
        _copiedOrderId = null;
        if (_sort == "rank" && !item.HasRanks)
            _sort = "price";

        SetHeader(item);
        ShowPanel(ResultsPanel);
        Refresh();

        Activate();
        Focus();
    }

    private void ClearStats()
    {
        StatLow.Text = StatMedian.Text = StatTotal.Text = StatInGame.Text = "—";
        FilterAllButton.Content = L.All;
        FilterOnlineButton.Content = L.Online;
        FilterInGameButton.Content = L.InGame;
        UpdateFooter();
    }

    private void SetHeader(MarketItem item)
    {
        TitleText.Text = item.DisplayName;
        TitleText.ToolTip = item.DisplayName;
        DucatsText.Text = item.Ducats is > 0 ? L.Ducats(item.Ducats.Value) : "";

        var rankVisibility = item.HasRanks ? Visibility.Visible : Visibility.Collapsed;
        SubtitleText.Text = item.HasRanks ? L.Ranks(item.MaxRank!.Value) : "";
        SubtitleText.Visibility = rankVisibility;
        RankPanel.Visibility = rankVisibility;
        SortRankOption.Visibility = rankVisibility;

        DetailHeader.Visibility = item.HasDetails ? Visibility.Visible : Visibility.Collapsed;
        DetailHeader.Width = OrderRow.DetailColumnWidth(item);
        DetailHeader.Text = item.HasRanks && !item.HasSubtypes && !item.HasCharges && !item.HasStars ? L.RankHeader
            : item.HasSubtypes && !item.HasRanks && !item.HasCharges && !item.HasStars ? L.TypeHeader
            : L.DetailsHeader;

        BuildVariantChips(item);
    }

    /// <summary>One chip per separately priced variant (Intact, Exceptional, Flawless, Radiant...) plus Any.</summary>
    private void BuildVariantChips(MarketItem item)
    {
        VariantChips.Children.Clear();
        VariantPanel.Visibility = item.HasSubtypes ? Visibility.Visible : Visibility.Collapsed;
        if (!item.HasSubtypes)
            return;

        VariantLabel.Text = item.Subtypes!.Any(s => s is "intact" or "radiant") ? L.Refinement : L.TypeLabel;
        foreach (var subtype in item.Subtypes!.Cast<string?>().Append(null))
        {
            var chip = new Button
            {
                Content = subtype == null ? L.Any : L.SubtypeName(subtype) ?? MarketItem.SubtypeLabel(subtype),
                Tag = subtype ?? AnySubtype,
                Style = (Style)FindResource("ChipButton"),
                Margin = new Thickness(0, 2, 4, 2),
                Focusable = false
            };
            chip.Click += VariantChip_Click;
            VariantChips.Children.Add(chip);
        }
    }

    private void VariantChip_Click(object sender, RoutedEventArgs e)
    {
        if (_item == null || (sender as Button)?.Tag is not string tag)
            return;
        _subtype = tag == AnySubtype ? null : tag;
        Refresh();
    }

    /// <summary>Rebuild stats, filter counts and the seller list from the current state.</summary>
    private void Refresh()
    {
        if (_item == null)
            return;

        int? max = _item.MaxRank;
        var matching = _orders
            .Where(o => _rank == null || o.Rank == _rank)
            .Where(o => _subtype == null || o.Subtype == _subtype)
            .ToList();
        var inGame = matching.Where(o => o.User?.Status == "ingame").ToList();
        var online = matching.Where(o => o.User?.Status is "ingame" or "online").ToList();

        StatLow.Text = inGame.Count > 0 ? $"{inGame.Min(o => o.Platinum)}p" : "—";
        StatMedian.Text = inGame.Count > 0 ? $"{Median(inGame.Select(o => o.Platinum))}p" : "—";
        StatTotal.Text = matching.Count.ToString();
        StatInGame.Text = inGame.Count.ToString();

        FilterAllButton.Content = $"{L.All} {matching.Count}";
        FilterOnlineButton.Content = $"{L.Online} {online.Count}";
        FilterInGameButton.Content = $"{L.InGame} {inGame.Count}";
        StyleSegment(FilterAllButton, _filter == "all");
        StyleSegment(FilterOnlineButton, _filter == "online");
        StyleSegment(FilterInGameButton, _filter == "ingame");

        if (_item.HasRanks && max is int m)
        {
            RankLabel.Text = _rank == null ? L.AnyRank : L.RankOf(_rank.Value, m);
            RankHint.Text = _rank == null ? L.ShowingRanks(m)
                : _rank == m ? L.MaxRank
                : _rank == 0 ? L.Unranked
                : L.BelowMax(m - _rank.Value);
            StyleChip(RankAnyButton, _rank == null);
            StyleChip(RankMaxButton, _rank == m);
        }

        foreach (var chip in VariantChips.Children.OfType<Button>())
            StyleChip(chip, (chip.Tag as string) == (_subtype ?? AnySubtype));

        SortText.Text = SortLabel(_sort);

        var shown = _filter switch
        {
            "ingame" => inGame,
            "online" => online,
            _ => matching
        };

        IEnumerable<MarketOrder> sorted = _sort switch
        {
            "price-desc" => shown.OrderByDescending(o => o.Platinum).ThenBy(StatusOrder),
            "rank" => shown.OrderByDescending(o => o.Rank ?? -1).ThenBy(o => o.Platinum).ThenBy(StatusOrder),
            "newest" => shown.OrderByDescending(o => o.CreatedAt),
            _ => shown.OrderBy(o => o.Platinum).ThenBy(StatusOrder)
        };

        var item = _item;
        _rows = sorted.Select((o, i) => new OrderRow(o, item, i % 2 == 1)).ToList();

        var selected = _rows.FirstOrDefault(r => r.Order.Id == _selectedOrderId)
                       ?? _rows.FirstOrDefault(r => r.Status == "ingame")
                       ?? _rows.FirstOrDefault();
        foreach (var r in _rows)
        {
            r.IsSelected = r == selected;
            r.IsCopied = r.Order.Id == _copiedOrderId;
        }
        _selectedOrderId = selected?.Order.Id;

        RowsList.ItemsSource = _rows;
        if (_rows.Count > 0)
            RowsList.ScrollIntoView(_rows[0]);

        if (_rows.Count > 0)
            ShowListMessage(null);
        else if (matching.Count == 0 && (_rank != null || _subtype != null))
            ShowListMessage(L.NoSellersForChoice);
        else if (matching.Count == 0)
            ShowListMessage(L.NobodySelling);
        else
            ShowListMessage(L.NoSellersWithStatus);

        UpdateFooter();
    }

    private static int StatusOrder(MarketOrder o) => o.User?.Status switch
    {
        "ingame" => 0,
        "online" => 1,
        _ => 2
    };

    private static int Median(IEnumerable<int> values)
    {
        var s = values.OrderBy(v => v).ToList();
        int k = s.Count / 2;
        return s.Count % 2 == 1 ? s[k] : (int)Math.Round((s[k - 1] + s[k]) / 2.0);
    }

    private static string SortLabel(string sort) => sort switch
    {
        "price-desc" => L.PriceHighFirst,
        "rank" => L.RankHighestFirst,
        "newest" => L.NewestFirst,
        _ => L.PriceLowFirst
    };

    private void ShowListMessage(string? message)
    {
        ListMessage.Text = message ?? "";
        ListMessage.Visibility = message == null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateFooter()
    {
        var copied = _rows.FirstOrDefault(r => r.IsCopied);
        FooterNote.Text = copied != null
            ? L.WhisperCopied(copied.User)
            : L.BCopiesWhisper;
        FooterNote.Foreground = Res(copied != null ? "AccentText" : "Muted");
    }

    private Brush Res(string key) => (Brush)FindResource(key);

    private void StyleSegment(Button button, bool on)
    {
        button.Background = on ? Res("Selected") : Brushes.Transparent;
        button.Foreground = Res(on ? "TextStrong" : "Muted");
    }

    private void StyleChip(Button button, bool on)
    {
        button.BorderBrush = Res(on ? "Accent" : "ControlBorder");
        button.Foreground = Res(on ? "AccentText" : "Soft");
    }

    // ---- Filters, rank, sort ---------------------------------------------------------

    private void Filter_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is string filter)
        {
            _filter = filter;
            Refresh();
        }
    }

    private void RankDown_Click(object sender, RoutedEventArgs e) => StepRank(-1);

    private void RankUp_Click(object sender, RoutedEventArgs e) => StepRank(1);

    private void StepRank(int delta)
    {
        if (_item?.MaxRank is not int max || max <= 0)
            return;

        int current = _rank ?? (delta > 0 ? -1 : max + 1);
        _rank = Math.Clamp(current + delta, 0, max);
        Refresh();
    }

    private void RankAny_Click(object sender, RoutedEventArgs e)
    {
        _rank = null;
        Refresh();
    }

    private void RankMax_Click(object sender, RoutedEventArgs e)
    {
        if (_item?.MaxRank is int max)
        {
            _rank = max;
            Refresh();
        }
    }

    private void SortButton_Click(object sender, RoutedEventArgs e) => SortPopup.IsOpen = !SortPopup.IsOpen;

    private void SortOption_Click(object sender, RoutedEventArgs e)
    {
        SortPopup.IsOpen = false;
        if ((sender as Button)?.Tag is string sort)
        {
            _sort = sort;
            Refresh();
        }
    }

    // ---- Selection, copy, open site ---------------------------------------------------

    private void Row_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is OrderRow row)
            Select(row, scrollIntoView: false);
    }

    private void Buy_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is OrderRow row)
        {
            Select(row, scrollIntoView: false);
            Copy(row);
        }
    }

    private void Select(OrderRow row, bool scrollIntoView)
    {
        foreach (var r in _rows)
            r.IsSelected = r == row;
        _selectedOrderId = row.Order.Id;

        // Works with virtualization: the row may not have a container until it is scrolled to
        if (scrollIntoView)
            RowsList.ScrollIntoView(row);
    }

    private void MoveSelection(int delta)
    {
        if (_rows.Count == 0)
            return;

        int index = _rows.FindIndex(r => r.IsSelected);
        index = Math.Clamp(index < 0 ? 0 : index + delta, 0, _rows.Count - 1);
        Select(_rows[index], scrollIntoView: true);
    }

    private void Copy(OrderRow row)
    {
        if (_item == null)
            return;

        if (!TrySetClipboard(BuildWhisper(row.Order, _item)))
        {
            FooterNote.Text = L.ClipboardBusy;
            FooterNote.Foreground = Res("AccentText");
            return;
        }

        foreach (var r in _rows)
            r.IsCopied = r == row;
        _copiedOrderId = row.Order.Id;
        UpdateFooter();
    }

    /// <summary>The same message warframe.market copies for "Buy".</summary>
    public static string BuildWhisper(MarketOrder order, MarketItem item)
    {
        // "Lith A1 Relic (Radiant)", "Corrosive Projection (rank 5)"
        var qualifier = item.OrderQualifier(order);
        // Always in English, the same as warframe.market's own whisper, whatever the interface language
        var name = qualifier.Length == 0 ? item.Name : $"{item.Name} ({qualifier})";
        return $"/w {order.User?.IngameName} Hi! I want to buy: \"{name}\" for {order.Platinum} platinum. (warframe.market)";
    }

    private static bool TrySetClipboard(string text)
    {
        // The clipboard can be briefly locked by another app; retry a couple of times
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                Clipboard.SetText(text);
                return true;
            }
            catch (COMException)
            {
                System.Threading.Thread.Sleep(40);
            }
        }
        return false;
    }

    private void OpenSite_Click(object sender, RoutedEventArgs e) => OpenSite();

    private void OpenSite()
    {
        if (_item == null)
            return;

        try
        {
            Process.Start(new ProcessStartInfo(WarframeMarketAPI.ItemPageUrl(_item.Slug)) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Couldn't open browser: {ex.Message}");
        }

        CloseWithAnimation();
    }

    // ---- Manual search -------------------------------------------------------------

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = SearchBox.Text;
        SearchPlaceholder.Visibility = text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        int version = ++_suggestionVersion;
        List<MarketItem> items;
        try
        {
            items = await _api.SuggestAsync(text, 6);
        }
        catch
        {
            items = new List<MarketItem>();
        }

        if (version != _suggestionVersion)
            return;

        _suggestions = items.Select(i => new Suggestion(i)).ToList();
        if (_suggestions.Count > 0)
            _suggestions[0].IsSelected = true;

        SuggestionList.ItemsSource = _suggestions;
        SuggestionEmpty.Text = _api.CatalogueLoaded
            ? L.NoItemsMatch
            : L.CantLoadItemList;
        SuggestionEmpty.Visibility = _suggestions.Count == 0 && text.Trim().Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Down or Key.Up && _suggestions.Count > 0)
        {
            int index = _suggestions.FindIndex(s => s.IsSelected);
            index = Math.Clamp(index + (e.Key == Key.Down ? 1 : -1), 0, _suggestions.Count - 1);
            for (int i = 0; i < _suggestions.Count; i++)
                _suggestions[i].IsSelected = i == index;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            var pick = _suggestions.FirstOrDefault(s => s.IsSelected);
            if (pick != null)
            {
                _ = LoadItemAsync(pick.Item);
                return;
            }

            // Enter pressed before the suggestions arrived: look the text up right away
            var text = SearchBox.Text;
            if (text.Trim().Length == 0)
                return;
            try
            {
                var items = await _api.SuggestAsync(text, 1);
                if (items.Count > 0 && !_isClosed && SearchBox.Text == text)
                    _ = LoadItemAsync(items[0]);
            }
            catch
            {
                // the empty-result text already explains what went wrong
            }
        }
    }

    private void Suggestion_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is Suggestion suggestion)
            _ = LoadItemAsync(suggestion.Item);
    }
}
