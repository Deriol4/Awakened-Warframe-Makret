using System;
using System.Globalization;

namespace AwakenedWarframeMarket.Services;

public enum AppLanguage
{
    English,
    Russian
}

/// <summary>
/// Interface texts in English and Russian. XAML reads them with {x:Static l:L.Name}, so the language is set
/// once at startup (before any window is created) and a change applies after a restart.
/// </summary>
public static class L
{
    public static AppLanguage Language { get; set; } = AppLanguage.English;

    public static bool IsRussian => Language == AppLanguage.Russian;

    /// <summary>"en" / "ru" from settings; anything else follows the Windows display language.</summary>
    public static AppLanguage Resolve(string? setting) => setting switch
    {
        "ru" => AppLanguage.Russian,
        "en" => AppLanguage.English,
        _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ? AppLanguage.Russian : AppLanguage.English
    };

    public static string ToSetting(AppLanguage language) => language == AppLanguage.Russian ? "ru" : "en";

    private static string T(string en, string ru) => IsRussian ? ru : en;

    // Russian plural forms: 1 минута, 2 минуты, 5 минут
    private static string Plural(long n, string one, string few, string many)
    {
        long mod100 = n % 100, mod10 = n % 10;
        if (mod100 is >= 11 and <= 14) return many;
        return mod10 switch { 1 => one, >= 2 and <= 4 => few, _ => many };
    }

    // ---- Main window -------------------------------------------------------------------

    public static string Exit => T("Exit", "Выход");
    public static string HideToTray => T("Hide to tray", "Свернуть в трей");
    public static string Settings => T("Settings", "Настройки");
    public static string Open => T("Open", "Открыть");
    public static string Starting => T("Starting…", "Запуск…");
    public static string PreparingRecognition => T("Preparing text recognition", "Подготовка распознавания текста");
    public static string PriceCheck => T("Price check", "Проверка цены");
    public static string PriceCheckHint => T("Hover an item in Warframe and press the hotkey", "Наведите курсор на предмет в Warframe и нажмите клавишу");
    public static string Search => T("Search", "Поиск");
    public static string SearchHint => T("Find any item by name, also ones you don't have", "Найти любой предмет по названию, даже если его у вас нет");
    public static string InTheOverlay => T("IN THE OVERLAY", "В ОКНЕ ЦЕН");
    public static string KeyCopyWhisper => T("Copy the whisper for the selected seller", "Скопировать сообщение выбранному продавцу");
    public static string KeyOpenOnSite => T("Open the item on warframe.market", "Открыть предмет на warframe.market");
    public static string KeyClose => T("Close (it also closes when you go back to the game)", "Закрыть (окно закроется и само, когда вы вернётесь в игру)");

    public static string StatusCouldntStart => T("Couldn't start", "Не удалось запустить");
    public static string StatusRecognitionFailed(string error) => T($"Text recognition failed to load. {error}", $"Не загрузилось распознавание текста. {error}");
    public static string InstallVcRedist => T("Install the Microsoft Visual C++ Redistributable (x64) and restart the app.",
        "Установите Microsoft Visual C++ Redistributable (x64) и перезапустите программу.");
    public static string StatusHotkeyUnavailable => T("Hotkey unavailable", "Клавиша занята");
    public static string StatusSearchHotkeyUnavailable => T("Search hotkey unavailable", "Клавиша поиска занята");
    public static string HotkeyTaken(string hotkey) => T($"{hotkey} is taken by another app. Pick another one in Settings.",
        $"{hotkey} уже занята другой программой. Выберите другую в настройках.");
    public static string StatusReady => T("Ready", "Готово");
    public static string WorksInAnyWindow(string hotkey) => T($"{hotkey} works in any window", $"{hotkey} работает в любом окне");
    public static string HoverAndPress(string hotkey) => T($"Hover an item in Warframe and press {hotkey}", $"Наведите курсор на предмет в Warframe и нажмите {hotkey}");
    public static string StatusWaitingForWarframe => T("Waiting for Warframe", "Ожидание Warframe");
    public static string WorksWhileWarframeInFront(string hotkey) => T($"{hotkey} works while Warframe is in front", $"{hotkey} работает, когда Warframe на переднем плане");

    // ---- Settings ----------------------------------------------------------------------

    public static string CloseWithoutSaving => T("Close without saving", "Закрыть без сохранения");
    public static string Close => T("Close", "Закрыть");
    public static string Cancel => T("Cancel", "Отмена");
    public static string Save => T("Save", "Сохранить");
    public static string PriceCheckHotkey => T("Price check hotkey", "Клавиша проверки цены");
    public static string PriceCheckHotkeyHint => T("Click the field and press a combination: Ctrl or Alt with a letter or digit, or an F-key (e.g. Alt + X, Ctrl + F5)",
        "Нажмите на поле и затем сочетание клавиш: Ctrl или Alt с буквой или цифрой, или F-клавишу (например, Alt + X, Ctrl + F5)");
    public static string SearchHotkey => T("Search hotkey", "Клавиша поиска");
    public static string SearchHotkeyHint => T("Opens the search to find any item, also ones you don't have", "Открывает поиск любого предмета, даже если его у вас нет");
    public static string OnlyInWarframe => T("Only while Warframe is in front", "Только когда Warframe на переднем плане");
    public static string OnlyInWarframeHint => T("Other apps can use the same keys when you're not in the game", "Другие программы смогут использовать эти клавиши, пока вы не в игре");
    public static string InterfaceLanguage => T("Interface language", "Язык интерфейса");
    public static string InterfaceLanguageHint => T("Applies after the app restarts", "Применится после перезапуска программы");
    public static string GameLanguage => T("Game language", "Язык игры");
    public static string GameLanguageHint => T("The language Warframe shows item names in, so they can be read from the screen",
        "Язык, на котором Warframe показывает названия предметов: на нём их читает программа");
    public static string PressCombination => T("Press a key combination…", "Нажмите сочетание клавиш…");
    public static string EscKeepsCurrent => T("Esc to keep the current one", "Esc — оставить как было");
    public static string AlreadySearchHotkey => T("That's already the search hotkey", "Это уже клавиша поиска");
    public static string AlreadyPriceHotkey => T("That's already the price check hotkey", "Это уже клавиша проверки цены");
    public static string CouldntSaveSettings(string folder, string error) => T($"Couldn't save settings to {folder}: {error}", $"Не удалось сохранить настройки в {folder}: {error}");

    // Hotkey rules
    public static string UseLetterDigitOrF => T("Use a letter, a digit or an F-key", "Используйте букву, цифру или F-клавишу");
    public static string F12Reserved => T("F12 is reserved by Windows", "F12 занята Windows");
    public static string AltF4Closes => T("Alt + F4 closes windows", "Alt + F4 закрывает окна");
    public static string AddCtrlOrAlt => T("Add Ctrl or Alt: a letter alone or with Shift fires while you type in chat",
        "Добавьте Ctrl или Alt: одна буква или буква с Shift сработает, пока вы пишете в чат");
    public static string NoMovementKeys => T("Ctrl or Shift with W, A, S, D fires while you crouch or sprint in the game",
        "Ctrl или Shift с W, A, S, D сработает, когда вы приседаете или бежите в игре");

    // ---- Overlay -----------------------------------------------------------------------

    public static string CloseEsc => T("Close (Esc)", "Закрыть (Esc)");
    public static string CloseOverlay => T("Close overlay", "Закрыть окно");
    public static string ReadingItem => T("Reading the item under the cursor", "Читаю предмет под курсором");
    public static string LookingUpItem => T("Looking up the item", "Ищу предмет");
    public static string LoadingSellers => T("Loading sellers", "Загружаю продавцов");
    public static string LoadingSellersDots => T("Loading sellers…", "Загружаю продавцов…");
    public static string SomethingWentWrong => T("Something went wrong", "Что-то пошло не так");
    public static string EscToClose => T("Esc to close", "Esc — закрыть");
    public static string CouldntReadName => T("Couldn't read the item name", "Не удалось прочитать название");
    public static string NotFoundOnMarket => T("Not found on warframe.market", "Не найдено на warframe.market");
    public static string FindAnItem => T("Find an item", "Найти предмет");
    public static string ReadFromScreen => T("Read from screen: ", "Прочитано с экрана: ");
    public static string Nothing => T("nothing", "ничего");
    public static string UntradeableHint => T("Untradeable items (Umbral mods, some Primed mods) aren't on warframe.market.",
        "Непередаваемых предметов (моды Умбра, некоторые Прайм-моды) нет на warframe.market.");
    public static string SearchItem => T("Search item", "Поиск предмета");
    public static string TypeToCorrect => T("Type to correct the name", "Исправьте название");
    public static string TypeAnyPart => T("Type any part of the name", "Введите любую часть названия");
    public static string ClosestItems => T("CLOSEST ITEMS", "ПОХОЖИЕ ПРЕДМЕТЫ");
    public static string NoItemsMatch => T("No items match. Check the spelling.", "Ничего не найдено. Проверьте написание.");
    public static string CantLoadItemList => T("Can't load the item list: no connection to warframe.market", "Не удалось загрузить список предметов: нет связи с warframe.market");
    public static string SearchKeys => T("↑↓ to choose · Enter to search · Esc to close", "↑↓ — выбрать · Enter — открыть · Esc — закрыть");
    public static string CouldntReachMarket => T("Couldn't reach warframe.market. Check your internet connection and try again.",
        "Нет связи с warframe.market. Проверьте подключение к интернету и попробуйте ещё раз.");
    public static string CouldntLoadSellersFor(string item, string reason) => T($"Couldn't load sellers for {item}.\n{reason}", $"Не удалось загрузить продавцов для «{item}».\n{reason}");
    public static string CouldntLoadSellers(string reason) => T($"Couldn't load sellers.\n{reason}", $"Не удалось загрузить продавцов.\n{reason}");
    public static string CouldntCaptureScreen(string error) => T($"Couldn't capture the screen: {error}", $"Не удалось сделать снимок экрана: {error}");

    public static string Refinement => T("Refinement", "Улучшение");
    public static string TypeLabel => T("Type", "Тип");
    public static string Any => T("Any", "Любой");
    public static string Max => T("Max", "Макс");
    public static string LowerRank => T("Lower rank", "Ранг ниже");
    public static string RaiseRank => T("Raise rank", "Ранг выше");
    public static string AnyRank => T("Any rank", "Любой ранг");
    public static string RankOf(int rank, int max) => T($"Rank {rank} of {max}", $"Ранг {rank} из {max}");
    public static string ShowingRanks(int max) => T($"showing ranks 0–{max}", $"ранги 0–{max}");
    public static string MaxRank => T("max rank", "макс. ранг");
    public static string Unranked => T("unranked", "без ранга");
    public static string BelowMax(int n) => T($"{n} below max", $"на {n} ниже макс.");
    public static string Ranks(int max) => T($"Ranks 0–{max}", $"Ранги 0–{max}");
    public static string Ducats(int n) => T($"{n} ducats", $"{n} {Plural(n, "дукат", "дуката", "дукатов")}");

    public static string LowestInGame => T("Lowest in game", "Минимум в игре");
    public static string MedianInGame => T("Median in game", "Медиана в игре");
    public static string Listings => T("Listings", "Лотов");
    public static string InGameNow => T("In game now", "Сейчас в игре");
    public static string Sort => T("Sort", "Сортировка");
    public static string PriceLowFirst => T("Price, low first", "Цена, сначала низкая");
    public static string PriceHighFirst => T("Price, high first", "Цена, сначала высокая");
    public static string RankHighestFirst => T("Rank, highest first", "Ранг, сначала высокий");
    public static string NewestFirst => T("Newest first", "Сначала новые");
    public static string All => T("All", "Все");
    public static string Online => T("Online", "Онлайн");
    public static string InGame => T("In game", "В игре");
    public static string Offline => T("Offline", "Не в сети");
    public static string Listed => T("LISTED", "ВЫСТАВЛЕН");
    public static string PriceHeader => T("PRICE", "ЦЕНА");
    public static string QtyHeader => T("QTY", "КОЛ.");
    public static string RankHeader => T("RANK", "РАНГ");
    public static string TypeHeader => T("TYPE", "ТИП");
    public static string DetailsHeader => T("DETAILS", "ДЕТАЛИ");
    public static string SellerHeader => T("SELLER", "ПРОДАВЕЦ");
    public static string OpenOnSiteTooltip => T("Open this item on warframe.market (T)", "Открыть предмет на warframe.market (T)");
    public static string OpenOnSite => T("Open on site", "Открыть на сайте");
    public static string NoSellersForChoice => T("No sellers for this choice. Try Any.", "Нет продавцов с таким выбором. Попробуйте «Любой».");
    public static string NobodySelling => T("Nobody is selling this item right now.", "Сейчас этот предмет никто не продаёт.");
    public static string NoSellersWithStatus => T("No sellers with this status right now.", "Сейчас нет продавцов с таким статусом.");
    public static string WhisperCopied(string user) => T($"Whisper to {user} copied, paste it in chat", $"Сообщение для {user} скопировано, вставьте его в чат");
    public static string BCopiesWhisper => T("B copies the whisper for the selected seller", "B — скопировать сообщение выбранному продавцу");
    public static string ClipboardBusy => T("Couldn't access the clipboard, try again", "Не удалось получить доступ к буферу обмена, попробуйте ещё раз");
    public static string Buy => T("Buy", "Купить");
    public static string Copied => T("Copied", "Скопировано");
    public static string CopyWhisperTo(string user) => T($"Copy whisper to {user}", $"Скопировать сообщение для {user}");
    public static string ChargesShort => T("ch", "зар.");

    /// <summary>Relic refinements have Russian names in the game; other variants keep the market's English label.</summary>
    public static string? SubtypeName(string subtype) => !IsRussian ? null : subtype switch
    {
        "intact" => "Нетронутая",
        "exceptional" => "Необычная",
        "flawless" => "Бесподобная",
        "radiant" => "Сияющая",
        _ => null
    };

    public static string Ago(TimeSpan diff)
    {
        if (diff.TotalMinutes < 1) return T("just now", "только что");
        if (diff.TotalMinutes < 60) return T($"{(int)diff.TotalMinutes}m ago", $"{(int)diff.TotalMinutes} мин назад");
        if (diff.TotalHours < 24) return T($"{(int)diff.TotalHours}h ago", $"{(int)diff.TotalHours} ч назад");
        if (diff.TotalDays < 30) return T($"{(int)diff.TotalDays}d ago", $"{(int)diff.TotalDays} дн назад");
        if (diff.TotalDays < 365) return T($"{(int)(diff.TotalDays / 30)}mo ago", $"{(int)(diff.TotalDays / 30)} мес назад");
        return T($"{(int)(diff.TotalDays / 365)}y ago", $"{(int)(diff.TotalDays / 365)} г назад");
    }

    // ---- Errors ------------------------------------------------------------------------

    public static string RateLimited => T("warframe.market is limiting requests right now. Try again in a minute.", "warframe.market сейчас ограничивает запросы. Попробуйте через минуту.");
    public static string ItemNotOnMarket => T("warframe.market doesn't have this item.", "На warframe.market нет такого предмета.");
    public static string MarketProblems => T("warframe.market is having problems right now. Try again later.", "У warframe.market сейчас проблемы. Попробуйте позже.");
    public static string NoConnection => T("Couldn't reach warframe.market. Check your internet connection.", "Нет связи с warframe.market. Проверьте подключение к интернету.");
    public static string TooSlow => T("warframe.market took too long to answer. Try again.", "warframe.market слишком долго не отвечает. Попробуйте ещё раз.");
    public static string UnknownFormat => T("warframe.market answered in a format this version doesn't understand. The app may need an update.",
        "warframe.market ответил в непонятном этой версии формате. Возможно, программу нужно обновить.");
    public static string Unexpected(string error) => T($"Something unexpected went wrong: {error}", $"Непредвиденная ошибка: {error}");
    public static string MustClose(string error) => T($"Awakened Warframe Market has to close because of an unexpected error:\n\n{error}",
        $"Awakened Warframe Market придётся закрыть из-за непредвиденной ошибки:\n\n{error}");
    public static string KeepsRunning(string error) => T($"Something went wrong: {error}\n\nThe app keeps running. If this repeats, restart it.",
        $"Что-то пошло не так: {error}\n\nПрограмма продолжает работать. Если ошибка повторится, перезапустите её.");
}
