using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AwakenedWarframeMarket.Services;

public class ScreenCapture
{
    private readonly bool _debugMode;

    public ScreenCapture(bool debugMode = false)
    {
        _debugMode = debugMode;
    }

    public Bitmap CaptureItemCell()
    {
        var cursor = Cursor.Position;
        var screen = Screen.FromPoint(cursor);

        // Capture 11% width x 20% height of screen, cursor at center
        int captureWidth = (int)(screen.Bounds.Width * 0.11);
        int captureHeight = (int)(screen.Bounds.Height * 0.20);

        // Offset to center cursor in capture area
        int offsetX = -captureWidth / 2;
        int offsetY = -captureHeight / 2;

        int x = cursor.X + offsetX;
        int y = cursor.Y + offsetY;

        // Clamp to screen bounds
        x = Math.Clamp(x, screen.Bounds.Left, screen.Bounds.Right - captureWidth);
        y = Math.Clamp(y, screen.Bounds.Top, screen.Bounds.Bottom - captureHeight);

        var bitmap = CaptureRegion(x, y, captureWidth, captureHeight);

        if (_debugMode)
        {
            SaveDebugImage(bitmap);
        }

        return bitmap;
    }

    private Bitmap CaptureRegion(int x, int y, int width, int height)
    {
        var bitmap = new Bitmap(width, height);

        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(x, y, 0, 0, new Size(width, height));
        }

        return bitmap;
    }

    private void SaveDebugImage(Bitmap bitmap)
    {
        try
        {
            var debugDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug");
            Directory.CreateDirectory(debugDir);

            var filename = $"debug_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            var filepath = Path.Combine(debugDir, filename);

            bitmap.Save(filepath);
            System.Diagnostics.Debug.WriteLine($"Debug image saved: {filepath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving debug image: {ex.Message}");
        }
    }
}
