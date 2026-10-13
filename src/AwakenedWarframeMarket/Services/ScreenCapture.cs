using System;
using System.Drawing;
using System.Windows.Forms;

namespace AwakenedWarframeMarket.Services;

/// <summary>A screenshot around the cursor, where the cursor is inside it, and the UI scale (1 at 1440p).</summary>
public sealed record ItemCapture(Bitmap Image, Point Cursor, double UiScale) : IDisposable
{
    public void Dispose() => Image.Dispose();
}

public class ScreenCapture
{
    // Share of the screen captured around the cursor. A hovered mod card is enlarged to about 11% x 29% of
    // the screen, and its name is usually below the cursor (the cursor rests on the picture), sometimes above
    // (cursor on the description). The area has to hold the whole card wherever the cursor is on it.
    private const double Left = 0.08, Right = 0.08, Up = 0.13, Down = 0.20;

    public ItemCapture CaptureItemCell()
    {
        var cursor = Cursor.Position;
        var screen = Screen.FromPoint(cursor).Bounds;

        int x0 = Math.Max(screen.Left, cursor.X - (int)(screen.Width * Left));
        int x1 = Math.Min(screen.Right, cursor.X + (int)(screen.Width * Right));
        int y0 = Math.Max(screen.Top, cursor.Y - (int)(screen.Height * Up));
        int y1 = Math.Min(screen.Bottom, cursor.Y + (int)(screen.Height * Down));

        var bitmap = new Bitmap(x1 - x0, y1 - y0);
        try
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(x0, y0, 0, 0, bitmap.Size);
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }

        return new ItemCapture(bitmap, new Point(cursor.X - x0, cursor.Y - y0), screen.Height / 1440.0);
    }
}
