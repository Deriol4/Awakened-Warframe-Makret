using System;
using System.Drawing;
using System.Threading.Tasks;

namespace AwakenedWarframeMarket.Services;

public class PriceCheckService
{
    private readonly ScreenCapture _screenCapture;
    private readonly OCRService _ocrService;
    private readonly WarframeMarketAPI _marketApi;
    private readonly Func<bool> _isDebugEnabled;

    // Only one overlay at a time: a new price check replaces the open one
    private OverlayWindow? _current;

    // isDebugEnabled is read on every check, so toggling Debug Mode in Settings takes effect immediately
    public PriceCheckService(ScreenCapture screenCapture, OCRService ocrService, WarframeMarketAPI marketApi, Func<bool> isDebugEnabled)
    {
        _screenCapture = screenCapture;
        _ocrService = ocrService;
        _marketApi = marketApi;
        _isDebugEnabled = isDebugEnabled;
    }

    /// <summary>Called on the UI thread when the hotkey is pressed.</summary>
    public async Task CheckPriceAsync()
    {
        var debug = _isDebugEnabled();

        // Close the previous overlay and let it disappear before grabbing the screen
        if (_current != null)
        {
            _current.Close();
            _current = null;
            await Task.Delay(60);
        }

        // Capture first: the overlay must not end up in the screenshot
        Bitmap screenshot;
        try
        {
            screenshot = _screenCapture.CaptureItemCell();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Couldn't capture the screen: {ex.Message}", "Awakened Warframe Market");
            return;
        }

        var overlay = new OverlayWindow(_marketApi);
        _current = overlay;
        overlay.Closed += (_, _) =>
        {
            if (_current == overlay)
                _current = null;
        };
        overlay.ShowLoading("Reading the item under the cursor", "");
        overlay.Show();

        try
        {
            // OCR in the background
            var (ocr, debugDir) = await Task.Run(() =>
            {
                using (screenshot)
                {
                    var result = _ocrService.Recognize(screenshot, keepProcessedImage: debug);

                    string? dir = null;
                    try
                    {
                        if (debug)
                            dir = DebugDump.SaveOcr(screenshot, result);
                    }
                    finally
                    {
                        result.ProcessedImage?.Dispose();
                    }

                    return (result, dir);
                }
            });

            var itemName = ocr.Name;

            if (string.IsNullOrWhiteSpace(itemName))
            {
                if (debug)
                    overlay.SetDebugNote("OCR: nothing read");
                overlay.ShowSearch("", "Couldn't read the item name");
                return;
            }

            overlay.ShowLoading("Looking up the item", itemName);

            var match = await _marketApi.FindBestMatchAsync(itemName);
            DebugDump.SaveMatch(debugDir, itemName, match);

            if (debug)
                overlay.SetDebugNote($"OCR: \"{itemName}\" ({match?.Method ?? "no match"})");

            if (match == null && !_marketApi.CatalogueLoaded)
            {
                overlay.ShowError("Couldn't reach warframe.market. Check your connection and try again.");
                return;
            }

            if (match == null)
            {
                overlay.ShowSearch(itemName, "Couldn't match the name");
                return;
            }

            await overlay.LoadItemAsync(match.Item);
        }
        catch (Exception ex)
        {
            overlay.ShowError(ex.Message);
        }
    }
}
