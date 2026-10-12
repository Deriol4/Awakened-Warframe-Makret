using System;
using System.Threading.Tasks;

namespace AwakenedWarframeMarket.Services;

public class PriceCheckService
{
    private readonly ScreenCapture _screenCapture;
    private readonly OCRService _ocrService;
    private readonly WarframeMarketAPI _marketApi;

    // Only one overlay at a time: a new price check replaces the open one
    private OverlayWindow? _current;

    // True between the hotkey press and the moment the screenshot is taken; presses in that window are ignored
    private bool _capturing;

    public PriceCheckService(ScreenCapture screenCapture, OCRService ocrService, WarframeMarketAPI marketApi)
    {
        _screenCapture = screenCapture;
        _ocrService = ocrService;
        _marketApi = marketApi;
    }

    /// <summary>Called on the UI thread when the hotkey is pressed.</summary>
    public async Task CheckPriceAsync()
    {
        if (_capturing)
            return;

        ItemCapture capture;
        _capturing = true;
        try
        {
            // Close the previous overlay and let it disappear before grabbing the screen
            if (_current != null)
            {
                _current.Close();
                _current = null;
                await Task.Delay(60);
            }

            // Capture first: the overlay must not end up in the screenshot
            capture = _screenCapture.CaptureItemCell();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Couldn't capture the screen: {ex.Message}", "Awakened Warframe Market");
            return;
        }
        finally
        {
            _capturing = false;
        }

        OverlayWindow overlay;
        try
        {
            overlay = new OverlayWindow(_marketApi);
        }
        catch
        {
            capture.Dispose();
            throw;
        }

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
            // OCR in the background; the screenshot only lives in memory and is disposed right after.
            // If another check replaces this overlay meanwhile, the overlay ignores the late result.
            var itemName = await Task.Run(() =>
            {
                using (capture)
                    return _ocrService.RecognizeItemName(capture.Image, capture.Cursor);
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
                overlay.ShowError("Couldn't reach warframe.market. Check your internet connection and try again.");
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
            overlay.ShowError(FriendlyError.Describe(ex));
        }
    }
}
