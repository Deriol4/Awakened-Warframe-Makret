using System;
using System.Drawing;
using System.Windows.Forms;

namespace AwakenedWarframeMarket.Services;

public class ScreenCapture
{
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

        return CaptureRegion(x, y, captureWidth, captureHeight);
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
}
