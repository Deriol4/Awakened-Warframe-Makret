using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tesseract;

namespace AwakenedWarframeMarket.Services;

/// <summary>One text line found by Tesseract. RejectedReason is null when the line is usable.</summary>
public record OcrLine(string Text, Rectangle Bounds, float Confidence, string? RejectedReason = null);

public class OCRService : IDisposable
{
    // Lines below this confidence are treated as OCR hallucinations (garbage like "WAL T -").
    private const float MinConfidence = 40f;

    private TesseractEngine? _engine;
    private readonly string _tessdataPath;
    private bool _disposed;

    // TesseractEngine is not thread-safe: one recognition at a time, and never during Dispose
    private readonly object _engineLock = new();

    public OCRService()
    {
        _tessdataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
    }

    /// <summary>Loads the English model that ships next to the exe. Slow (a second or so), so it runs in the background.</summary>
    public Task InitializeAsync() => Task.Run(() =>
    {
        if (!File.Exists(Path.Combine(_tessdataPath, "eng.traineddata")))
            throw new FileNotFoundException("tessdata\\eng.traineddata is missing next to the app. Reinstall or rebuild the app.");

        var engine = new TesseractEngine(_tessdataPath, "eng", EngineMode.Default);
        engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 '-");
        lock (_engineLock)
            _engine = engine;
    });

    /// <summary>The item name nearest to the cursor, or "" if nothing readable was found.</summary>
    public string RecognizeItemName(Bitmap image, Point cursor)
    {
        using var processed = PreprocessImage(image);
        using var pix = BitmapToPix(processed);

        List<OcrLine> lines;
        lock (_engineLock)
        {
            if (_disposed)
                return "";
            if (_engine == null)
                throw new InvalidOperationException("OCR engine not initialized. Call InitializeAsync first.");

            using var page = _engine.Process(pix);
            lines = ReadLines(page);
        }

        return PickItemNameBlock(lines, cursor)?.Name ?? "";
    }

    private Bitmap PreprocessImage(Bitmap original)
    {
        var processed = new Bitmap(original.Width, original.Height);

        using (var g = Graphics.FromImage(processed))
        {
            // Convert to grayscale with increased contrast
            var colorMatrix = new ColorMatrix(new float[][]
            {
                new float[] { 0.5f, 0.5f, 0.5f, 0, 0 },
                new float[] { 0.5f, 0.5f, 0.5f, 0, 0 },
                new float[] { 0.5f, 0.5f, 0.5f, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 },
                new float[] { -0.2f, -0.2f, -0.2f, 0, 1 }
            });

            using var attributes = new ImageAttributes();
            attributes.SetColorMatrix(colorMatrix);

            g.DrawImage(original,
                new Rectangle(0, 0, original.Width, original.Height),
                0, 0, original.Width, original.Height,
                GraphicsUnit.Pixel, attributes);
        }

        return processed;
    }

    private Pix BitmapToPix(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        stream.Position = 0;
        return Pix.LoadFromMemory(stream.ToArray());
    }

    // ---- Reading lines -------------------------------------------------------------

    private List<OcrLine> ReadLines(Page page)
    {
        var result = new List<OcrLine>();

        using var iter = page.GetIterator();
        iter.Begin();
        do
        {
            if (!iter.TryGetBoundingBox(PageIteratorLevel.TextLine, out var b))
                continue;

            var text = CleanItemName(iter.GetText(PageIteratorLevel.TextLine) ?? "");
            if (text.Length == 0)
                continue;

            var confidence = iter.GetConfidence(PageIteratorLevel.TextLine);
            var bounds = Rectangle.FromLTRB(b.X1, b.Y1, b.X2, b.Y2);

            result.Add(new OcrLine(text, bounds, confidence, GetRejectReason(text, confidence)));
        } while (iter.Next(PageIteratorLevel.TextLine));

        return result;
    }

    private static string? GetRejectReason(string text, float confidence)
    {
        if (confidence < MinConfidence)
            return $"low confidence ({confidence:0}%)";

        if (text.Length < 3)
            return "too short";

        // Counters like "2" or prices like "35 platinum"
        if (Regex.IsMatch(text, @"^\d+\s*(p|plat|platinum)?$", RegexOptions.IgnoreCase))
            return "looks like a number or price";

        return null;
    }

    // ---- Choosing the item name ----------------------------------------------------

    private sealed record NameBlock(string Name, Rectangle Bounds);

    /// <summary>
    /// Long names wrap onto several lines ("Hildryn Prime / Neuroptics / Blueprint"), so lines are first grouped
    /// into blocks. The label of an inventory cell is always at or below the cursor, so blocks that lie
    /// entirely above the cursor belong to the cell above and are skipped.
    /// </summary>
    private static NameBlock? PickItemNameBlock(List<OcrLine> lines, Point cursor)
    {
        var usable = lines
            .Where(l => l.RejectedReason == null)
            .OrderBy(l => l.Bounds.Top)
            .ToList();

        if (usable.Count == 0)
            return null;

        // Group: a line continues a block if it sits right under that block's last line and overlaps it horizontally
        var blocks = new List<List<OcrLine>>();
        foreach (var line in usable)
        {
            var target = blocks.FirstOrDefault(b => Continues(b[^1], line));
            if (target != null)
                target.Add(line);
            else
                blocks.Add(new List<OcrLine> { line });
        }

        int centerX = cursor.X;
        int centerY = cursor.Y;

        var candidates = blocks
            .Select(b => new { Lines = b, Bounds = UnionBounds(b) })
            .ToList();

        // Label is at or below the cursor; fall back to everything if that leaves nothing
        var belowCursor = candidates.Where(c => c.Bounds.Bottom >= centerY).ToList();
        if (belowCursor.Count > 0)
            candidates = belowCursor;

        var best = candidates
            .OrderBy(c => DistanceToPoint(c.Bounds, centerX, centerY))
            .First();

        var name = string.Join(" ", best.Lines.Select(l => l.Text));
        return new NameBlock(name, best.Bounds);
    }

    private static bool Continues(OcrLine prev, OcrLine next)
    {
        int height = Math.Max(prev.Bounds.Height, next.Bounds.Height);

        // Vertical: directly underneath (allowing slight overlap of descenders/ascenders), not on the same row
        int gap = next.Bounds.Top - prev.Bounds.Bottom;
        if (gap > height * 0.9 || gap < -height * 0.25)
            return false;

        // Horizontal: wrapped lines of one label are centered on each other
        int overlap = Math.Min(prev.Bounds.Right, next.Bounds.Right) - Math.Max(prev.Bounds.Left, next.Bounds.Left);
        int narrower = Math.Min(prev.Bounds.Width, next.Bounds.Width);
        return overlap >= narrower * 0.5;
    }

    private static Rectangle UnionBounds(List<OcrLine> lines) =>
        lines.Select(l => l.Bounds).Aggregate(Rectangle.Union);

    private static double DistanceToPoint(Rectangle r, int x, int y)
    {
        int dx = x < r.Left ? r.Left - x : x > r.Right ? x - r.Right : 0;
        int dy = y < r.Top ? r.Top - y : y > r.Bottom ? y - r.Bottom : 0;
        return Math.Sqrt((double)dx * dx + (double)dy * dy);
    }

    private string CleanItemName(string text)
    {
        // Remove common OCR artifacts
        text = text.Trim();
        text = Regex.Replace(text, @"[|\\\/\[\]{}]", "");
        text = Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }

    public void Dispose()
    {
        lock (_engineLock)
        {
            if (!_disposed)
            {
                _engine?.Dispose();
                _engine = null;
                _disposed = true;
            }
        }
        GC.SuppressFinalize(this);
    }
}
