using System;
using System.Drawing;
using System.IO;
using System.Text;

namespace AwakenedWarframeMarket.Services;

/// <summary>
/// Writes what the app "saw" to disk so OCR and matching problems can be inspected.
/// Used only when Debug Mode is on. Each price check gets its own folder:
/// &lt;app folder&gt;\debug\yyyyMMdd_HHmmss_fff\
/// </summary>
public static class DebugDump
{
    public static string RootDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug");

    /// <returns>The folder that was created, or null if writing failed.</returns>
    public static string? SaveOcr(Bitmap capture, OcrResult ocr)
    {
        try
        {
            var dir = Path.Combine(RootDirectory, DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            Directory.CreateDirectory(dir);

            // 1. Exactly what was grabbed from the screen
            capture.Save(Path.Combine(dir, "1_capture.png"));

            // 2. What Tesseract actually received after preprocessing
            ocr.ProcessedImage?.Save(Path.Combine(dir, "2_processed.png"));

            // 3. Capture with every detected line outlined
            SaveAnnotated(capture, ocr, Path.Combine(dir, "3_annotated.png"));

            // 4. Text report
            var sb = new StringBuilder();
            sb.AppendLine($"Capture: {capture.Width}x{capture.Height} px (the cursor is at the image center: {capture.Width / 2},{capture.Height / 2})");
            sb.AppendLine($"Name sent to the market search: \"{ocr.Name}\"");
            sb.AppendLine();
            sb.AppendLine("Lines found by Tesseract  [x,y WxH]  confidence  text");
            foreach (var l in ocr.Lines)
            {
                var status = l.RejectedReason == null ? "" : $"   <- rejected: {l.RejectedReason}";
                sb.AppendLine($"  [{l.Bounds.Left},{l.Bounds.Top} {l.Bounds.Width}x{l.Bounds.Height}]  {l.Confidence,3:0}%  \"{l.Text}\"{status}");
            }
            if (ocr.Lines.Count == 0)
                sb.AppendLine("  (Tesseract found no text at all)");

            File.WriteAllText(Path.Combine(dir, "4_ocr.txt"), sb.ToString());
            return dir;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error writing debug dump: {ex.Message}");
            return null;
        }
    }

    public static void SaveMatch(string? dir, string ocrText, ItemMatch? match)
    {
        if (dir == null)
            return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Searched for: \"{ocrText}\"");

            if (match == null)
            {
                sb.AppendLine("Result: nothing found (no exact match, nothing close enough, no unique partial match)");
            }
            else
            {
                sb.AppendLine($"Result: \"{match.Item.Name}\"  (slug: {match.Item.Slug})");
                sb.AppendLine($"Method: {match.Method}" + (match.Distance != null ? $", edit distance {match.Distance}" : ""));

                if (match.Alternatives.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Closest candidates:");
                    foreach (var alt in match.Alternatives)
                        sb.AppendLine($"  {alt}");
                }
            }

            File.WriteAllText(Path.Combine(dir, "5_match.txt"), sb.ToString());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error writing match debug info: {ex.Message}");
        }
    }

    private static void SaveAnnotated(Bitmap capture, OcrResult ocr, string path)
    {
        // Line bounds are in processed-image coordinates; scale in case that image differs in size
        float sx = ocr.ProcessedImage != null ? (float)capture.Width / ocr.ProcessedImage.Width : 1f;
        float sy = ocr.ProcessedImage != null ? (float)capture.Height / ocr.ProcessedImage.Height : 1f;

        Rectangle Scale(Rectangle r) =>
            new((int)(r.X * sx), (int)(r.Y * sy), (int)(r.Width * sx), (int)(r.Height * sy));

        using var img = new Bitmap(capture);
        using var g = Graphics.FromImage(img);
        using var font = new Font("Segoe UI", 8f);
        using var usedPen = new Pen(Color.Yellow, 1);
        using var rejectedPen = new Pen(Color.Red, 1);
        using var chosenPen = new Pen(Color.Lime, 3);
        using var cursorPen = new Pen(Color.Cyan, 1);

        // Yellow = usable line, red = rejected line; the number is Tesseract's confidence
        foreach (var line in ocr.Lines)
        {
            var r = Scale(line.Bounds);
            g.DrawRectangle(line.RejectedReason == null ? usedPen : rejectedPen, r);
            g.DrawString($"{line.Confidence:0}%", font, Brushes.White, r.Left, Math.Max(0, r.Top - 13));
        }

        // Green = the block that became the item name
        if (ocr.ChosenBounds is { } chosen)
            g.DrawRectangle(chosenPen, Scale(chosen));

        // Cyan cross = cursor position
        int cx = img.Width / 2, cy = img.Height / 2;
        g.DrawLine(cursorPen, cx - 10, cy, cx + 10, cy);
        g.DrawLine(cursorPen, cx, cy - 10, cx, cy + 10);

        img.Save(path);
    }
}
