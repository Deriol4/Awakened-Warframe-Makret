using System;
using System.Threading.Tasks;
using AwakenedWarframeMarket.Models;

namespace AwakenedWarframeMarket.Services;

public class PriceCheckService
{
    private readonly ScreenCapture _screenCapture;
    private readonly OCRService _ocrService;
    private readonly WarframeMarketAPI _marketApi;

    public PriceCheckService(ScreenCapture screenCapture, OCRService ocrService, WarframeMarketAPI marketApi)
    {
        _screenCapture = screenCapture;
        _ocrService = ocrService;
        _marketApi = marketApi;
    }

    public async Task CheckPriceAsync()
    {
        // Show overlay immediately with loading state
        OverlayWindow? overlay = null;

        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            overlay = new OverlayWindow();
            overlay.Show();
            overlay.ShowLoadingState("Scanning item...");
        });

        if (overlay == null) return;

        try
        {
            // Perform OCR in background
            var itemName = await Task.Run(() =>
            {
                using var screenshot = _screenCapture.CaptureItemCell();
                return _ocrService.RecognizeItemName(screenshot);
            });

            if (string.IsNullOrWhiteSpace(itemName))
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    overlay.ShowError("Could not detect item name.\nMake sure your cursor is over the item name."));
                return;
            }

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                overlay.UpdateStatus($"Searching for '{itemName}'..."));

            // Find item on market
            var urlName = await _marketApi.FindClosestItemNameAsync(itemName);

            if (urlName == null)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    overlay.ShowError($"Item '{itemName}' not found on warframe.market"));
                return;
            }

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                overlay.UpdateStatus($"Loading prices..."));

            // Get prices
            var prices = await _marketApi.GetItemPricesAsync(urlName);

            // Format item name for display
            var displayName = FormatItemName(urlName);

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                overlay.SetItemData(displayName, prices));
        }
        catch (Exception ex)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                overlay.ShowError($"Error: {ex.Message}"));
        }
    }

    private string FormatItemName(string urlName)
    {
        // Convert "arcane_blessing" to "Arcane Blessing"
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo
            .ToTitleCase(urlName.Replace("_", " "));
    }
}
