using System;
using System.Drawing;
using System.Threading.Tasks;

namespace AwakenedWarframeMarket.Services;

public class PriceCheckService
{
    private readonly ScreenCapture _screenCapture;
    private readonly OCRService _ocrService;
    private readonly WarframeMarketAPI _marketApi;

    // Only one overlay at a time: a new price check replaces the open one
    private OverlayWindow? _current;

    public PriceCheckService(ScreenCapture screenCapture, OCRService ocrService, WarframeMarketAPI marketApi)
    {
        _screenCapture = screenCapture;
        _ocrService = ocrService;
        _marketApi = marketApi;
    }

    /// <summary>Called on the UI thread when the hotkey is pressed.</summary>
    public async Task CheckPriceAsync()
    {
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
            // OCR in the background; the screenshot only lives in memory and is disposed right after
            var itemName = await Task.Run(() =>
            {
                using (screenshot)
                    return _ocrService.RecognizeItemName(screenshot);
            });

            if (string.IsNullOrWhiteSpace(itemName))
            {
                overlay.ShowSearch("", "Couldn't read the item name");
                return;
            }

            overlay.ShowLoading("Looking up the item", itemName);

            var match = await _marketApi.FindBestMatchAsync(itemName);

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
