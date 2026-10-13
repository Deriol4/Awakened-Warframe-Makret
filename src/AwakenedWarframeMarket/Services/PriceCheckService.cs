using System.Collections.Generic;
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

    /// <summary>Called on the UI thread when the search hotkey is pressed: the overlay opens in the search.</summary>
    public async Task OpenSearchAsync()
    {
        if (_capturing)
            return;

        _capturing = true;
        try
        {
            if (_current != null)
            {
                _current.Close();
                _current = null;
                await Task.Delay(60);
            }
        }
        finally
        {
            _capturing = false;
        }

        var overlay = new OverlayWindow(_marketApi);
        _current = overlay;
        overlay.Closed += (_, _) =>
        {
            if (_current == overlay)
                _current = null;
        };
        overlay.ShowFreeSearch();
        overlay.Show();
    }

    /// <summary>Called on the UI thread when the price check hotkey is pressed.</summary>
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
            System.Windows.MessageBox.Show(L.CouldntCaptureScreen(ex.Message), "Awakened Warframe Market");
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
        overlay.ShowLoading(L.ReadingItem, "");
        overlay.Show();

        try
        {
            // The screenshot only lives in memory and is disposed right after reading.
            // If another check replaces this overlay meanwhile, the overlay ignores the late result.
            ItemMatch? match = null;
            string guess = "";
            using (capture)
            {
                using var prepared = await Task.Run(() => OCRService.PrepareImage(capture));

                // A second, slower reading only when the first finds no item
                foreach (var mode in OCRService.Passes)
                {
                    // Both readings of this pass go to the matcher together
                    var candidates = new List<NameCandidate>();
                    foreach (var scale in OCRService.Scales)
                    {
                        var read = await Task.Run(() => _ocrService.ReadCandidates(prepared, capture.Cursor, mode, scale));
                        if (guess.Length == 0)
                            guess = read.BestGuess;
                        candidates.AddRange(read.Candidates);
                    }
                    if (candidates.Count == 0)
                        continue;

                    overlay.ShowLoading(L.LookingUpItem, guess);
                    match = await _marketApi.FindBestMatchAsync(candidates);
                    if (match != null || !_marketApi.CatalogueLoaded)
                        break;
                }
            }

            if (match == null && !_marketApi.CatalogueLoaded)
            {
                overlay.ShowError(L.CouldntReachMarket);
                return;
            }

            if (match == null)
            {
                overlay.ShowSearch(guess, guess.Length == 0 ? L.CouldntReadName : L.NotFoundOnMarket);
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
