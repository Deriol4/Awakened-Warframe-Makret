using System;
using System.Drawing;
using System.Windows.Forms;

namespace AwakenedWarframeMarket.Services;

/// <summary>A screenshot around the cursor and where the cursor is inside it.</summary>
public sealed record ItemCapture(Bitmap Image, Point Cursor) : IDisposable
{
    public void Dispose() => Image.Dispose();
}

public class ScreenCapture
{
    public ItemCapture CaptureItemCell()
    {
        var cursor = Cursor.Position;
        var screen = Screen.FromPoint(cursor).Bounds;

        // 11% of the screen width x 20% of its height, centered on the cursor
        int width = (int)(screen.Width * 0.11);
        int height = (int)(screen.Height * 0.20);

        // Near a screen edge the area is shifted to stay on screen, so the cursor is no longer in its center
        int x = Math.Clamp(cursor.X - width / 2, screen.Left, screen.Right - width);
        int y = Math.Clamp(cursor.Y - height / 2, screen.Top, screen.Bottom - height);

        var bitmap = new Bitmap(width, height);
        try
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(x, y, 0, 0, new Size(width, height));
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }

        return new ItemCapture(bitmap, new Point(cursor.X - x, cursor.Y - y));
    }
}
