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

/// <summary>
/// A piece of text that might be the item's name, with where it was found relative to the cursor.
/// Which one is the name is decided later, by checking every candidate against the item catalogue.
/// </summary>
/// <param name="Height">Typical letter height in pixels; the hovered item's name is printed larger than its neighbours'.</param>
/// <param name="Dx">Horizontal distance from the cursor to the text (0 when the text spans the cursor's column).</param>
/// <param name="Dy">Vertical distance from the cursor to the text.</param>
/// <param name="Above">The text lies entirely above the cursor.</param>
/// <param name="LeftoverWords">For a part of a line: how many real words of that line were left out (0 for whole lines).</param>
/// <param name="LinesBelow">Lines under it in the same block, like a mod's description under its name.</param>
/// <param name="WordBefore">For a part of a line: the word right before it ("Primed" in "Primed Shred"), else null.</param>
public sealed record NameCandidate(
    string Text, double Height, double Dx, double Dy, bool Above, double LeftoverWords, int LinesBelow, string? WordBefore = null)
{
    public bool IsPartOfLine => LeftoverWords > 0;

    /// <summary>
    /// Above the cursor, with nothing under it, while text of the same size sits under the cursor: in an inventory
    /// grid that's the label of the cell above (the hovered cell's label is the one below). Set by <see cref="OCRService.Analyze"/>.
    /// </summary>
    public bool LabelOfCellAbove { get; init; }
}

/// <summary>One word as Tesseract read it.</summary>
public sealed record OcrWord(string Text, Rectangle Bounds);

/// <summary>Everything read around the cursor in one pass.</summary>
/// <param name="BestGuess">Text most likely to be the name, for the manual search when nothing matches.</param>
public sealed record OcrResult(IReadOnlyList<NameCandidate> Candidates, string BestGuess);

public class OCRService : IDisposable
{
    // Lines below this confidence are treated as OCR hallucinations (garbage like "WAL T -").
    private const float MinConfidence = 40f;

    /// <summary>
    /// Page layouts to try, in order. Automatic layout reads most screens; sparse text finds labels it misses
    /// (for example white text on a bright mod card). The second pass only runs when the first finds no item.
    /// Each pass reads the image as it is and enlarged twice: small labels of an inventory grid (prime parts)
    /// come out much better enlarged, large mod names better as they are, so both readings are used together.
    /// </summary>
    public static readonly PageSegMode[] Passes = { PageSegMode.Auto, PageSegMode.SparseText };
    public static readonly int[] Scales = { 1, 2 };

    private TesseractEngine? _engine;
    private readonly string _tessdataPath;
    private bool _disposed;

    // TesseractEngine is not thread-safe: one recognition at a time, and never during Dispose
    private readonly object _engineLock = new();

    public OCRService()
    {
        _tessdataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
    }

    private const string LatinLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const string CyrillicLetters = "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя";

    /// <summary>
    /// Loads the recognition model for the game's language (eng or rus, shipped next to the exe), replacing
    /// the current one. Slow (a second or so), so it runs in the background.
    /// </summary>
    public Task InitializeAsync(AppLanguage gameLanguage) => Task.Run(() =>
    {
        var model = gameLanguage == AppLanguage.Russian ? "rus" : "eng";
        if (!File.Exists(Path.Combine(_tessdataPath, model + ".traineddata")))
            throw new FileNotFoundException($"tessdata\\{model}.traineddata is missing next to the app. Reinstall or rebuild the app.");

        var engine = new TesseractEngine(_tessdataPath, model, EngineMode.Default);
        // Russian names keep Latin letters in places ("Реликвия Акси A1") and use ':' and '()'
        var letters = gameLanguage == AppLanguage.Russian ? CyrillicLetters + LatinLetters : LatinLetters;
        var punctuation = gameLanguage == AppLanguage.Russian ? " '-:()" : " '-";
        engine.SetVariable("tessedit_char_whitelist", letters + "0123456789" + punctuation);

        TesseractEngine? old;
        lock (_engineLock)
        {
            if (_disposed)
            {
                engine.Dispose();
                return;
            }
            old = _engine;
            _engine = engine;
        }
        old?.Dispose();
    });

    // ---- Image -----------------------------------------------------------------------

    /// <summary>
    /// Hides the game's cursor and converts the capture to high-contrast grayscale. Warframe draws its own
    /// cursor into the picture, and Tesseract reads the arrow as stray letters glued to the name; painting it
    /// over with the surrounding color leaves at worst a gap that the catalogue matching can live with.
    /// </summary>
    public static Bitmap PrepareImage(ItemCapture capture)
    {
        using var copy = new Bitmap(capture.Image);
        HideCursor(copy, capture.Cursor, capture.UiScale);
        return ToGrayscale(copy);
    }

    private static void HideCursor(Bitmap image, Point cursor, double scale)
    {
        // The arrow's tip is the hotspot; at 1440p the arrow is about 26 x 36 pixels
        var area = Rectangle.Intersect(
            new Rectangle(cursor.X - (int)(3 * scale), cursor.Y - (int)(3 * scale), (int)(29 * scale), (int)(39 * scale)),
            new Rectangle(0, 0, image.Width, image.Height));
        if (area.Width <= 0 || area.Height <= 0)
            return;

        // Typical color of the strips just above and below the arrow
        var samples = new List<Color>();
        for (int x = area.Left; x < area.Right; x++)
        {
            for (int d = 1; d <= 4; d++)
            {
                if (area.Top - d >= 0) samples.Add(image.GetPixel(x, area.Top - d));
                if (area.Bottom - 1 + d < image.Height) samples.Add(image.GetPixel(x, area.Bottom - 1 + d));
            }
        }
        if (samples.Count == 0)
            return;

        static int Median(IEnumerable<int> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            return sorted[sorted.Count / 2];
        }

        var fill = Color.FromArgb(Median(samples.Select(c => (int)c.R)), Median(samples.Select(c => (int)c.G)), Median(samples.Select(c => (int)c.B)));
        using var g = Graphics.FromImage(image);
        using var brush = new SolidBrush(fill);
        g.FillRectangle(brush, area);
    }

    private static Bitmap ToGrayscale(Bitmap original)
    {
        var processed = new Bitmap(original.Width, original.Height);

        using (var g = Graphics.FromImage(processed))
        {
            // Grayscale with increased contrast
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

    private static Pix BitmapToPix(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        return Pix.LoadFromMemory(stream.ToArray());
    }

    // ---- Reading -----------------------------------------------------------------------

    private sealed class Line
    {
        public List<OcrWord> Words { get; } = new();
        public float Confidence { get; set; }
        public Rectangle Bounds => Words.Select(w => w.Bounds).Aggregate(Rectangle.Union);
        public string Text => string.Join(" ", Words.Select(w => w.Text));
    }

    /// <summary>
    /// Reads the prepared image (enlarged <paramref name="scale"/> times first) and lists every piece of text
    /// that could be the item name, in the prepared image's coordinates.
    /// </summary>
    public OcrResult ReadCandidates(Bitmap prepared, Point cursor, PageSegMode mode, int scale = 1)
    {
        using var scaled = scale == 1 ? null : Enlarge(prepared, scale);
        using var pix = BitmapToPix(scaled ?? prepared);

        List<Line> lines;
        lock (_engineLock)
        {
            if (_disposed)
                return new OcrResult(Array.Empty<NameCandidate>(), "");
            if (_engine == null)
                throw new InvalidOperationException("OCR engine not initialized. Call InitializeAsync first.");

            using var page = _engine.Process(pix, mode);
            lines = ReadLines(page);
        }

        if (scale != 1)
        {
            foreach (var line in lines)
            {
                for (int i = 0; i < line.Words.Count; i++)
                {
                    var b = line.Words[i].Bounds;
                    line.Words[i] = line.Words[i] with
                    {
                        Bounds = Rectangle.FromLTRB(b.Left / scale, b.Top / scale, b.Right / scale, b.Bottom / scale)
                    };
                }
            }
        }

        return Analyze(lines.Select(l => ((IReadOnlyList<OcrWord>)l.Words, l.Confidence)), cursor, prepared.Width);
    }

    private static Bitmap Enlarge(Bitmap image, int scale)
    {
        var result = new Bitmap(image.Width * scale, image.Height * scale);
        using var g = Graphics.FromImage(result);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        g.DrawImage(image, 0, 0, result.Width, result.Height);
        return result;
    }

    /// <summary>Turns the lines Tesseract read into name candidates. Separate from reading so it can be tested.</summary>
    public static OcrResult Analyze(IEnumerable<(IReadOnlyList<OcrWord> Words, float Confidence)> read, Point cursor, int imageWidth)
    {
        var lines = read.Where(l => l.Words.Count > 0).Select(l =>
        {
            var line = new Line { Confidence = l.Confidence };
            line.Words.AddRange(l.Words);
            return line;
        }).ToList();

        var usable = MergeRows(lines).SelectMany(SplitAtGaps).Where(IsUsable).ToList();
        var candidates = BuildCandidates(GroupBlocks(usable), cursor, imageWidth);

        candidates = CandidateRanking.MarkLabelsOfCellAbove(candidates);
        return new OcrResult(candidates, BestGuess(candidates));
    }

    private static List<Line> ReadLines(Page page)
    {
        var lines = new List<Line>();

        using var iter = page.GetIterator();
        iter.Begin();
        do
        {
            var line = new Line();
            var confidences = new List<float>();
            do
            {
                if (!iter.TryGetBoundingBox(PageIteratorLevel.Word, out var b))
                    continue;
                var text = CleanText(iter.GetText(PageIteratorLevel.Word) ?? "");
                if (text.Length == 0)
                    continue;
                line.Words.Add(new OcrWord(text, Rectangle.FromLTRB(b.X1, b.Y1, b.X2, b.Y2)));
                confidences.Add(iter.GetConfidence(PageIteratorLevel.Word));
            } while (iter.Next(PageIteratorLevel.TextLine, PageIteratorLevel.Word));

            if (line.Words.Count > 0)
            {
                line.Confidence = confidences.Average();
                lines.Add(line);
            }
        } while (iter.Next(PageIteratorLevel.TextLine));

        return lines;
    }

    private static bool IsUsable(Line line)
    {
        var text = line.Text;
        if (line.Confidence < MinConfidence || text.Length < 3)
            return false;
        // Counters like "2" or prices like "35 platinum"
        return !Regex.IsMatch(text, @"^\d+\s*(p|plat|platinum)?$", RegexOptions.IgnoreCase);
    }

    private static string CleanText(string text)
    {
        text = Regex.Replace(text.Trim(), @"[|\\\/\[\]{}]", "");
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    /// <summary>
    /// The opposite of <see cref="MergeRows"/>: in a grid Tesseract often reads the labels of neighbouring cells as
    /// one line ("Neuroptics Mirage Prime"). Words of one label are close together, so a wide gap ends a label.
    /// Without this, a two-line label glued to its neighbour can't be put back together.
    /// </summary>
    private static IEnumerable<Line> SplitAtGaps(Line line)
    {
        var current = new Line { Confidence = line.Confidence };
        OcrWord? previous = null;
        foreach (var word in line.Words.OrderBy(w => w.Bounds.Left))
        {
            if (previous != null)
            {
                int gap = word.Bounds.Left - previous.Bounds.Right;
                int height = Math.Max(word.Bounds.Height, previous.Bounds.Height);
                if (gap > 1.5 * height)
                {
                    yield return current;
                    current = new Line { Confidence = line.Confidence };
                }
            }
            current.Words.Add(word);
            previous = word;
        }
        if (current.Words.Count > 0)
            yield return current;
    }

    /// <summary>Tesseract sometimes splits one label into pieces on the same row ("Blood For L" + "ife").</summary>
    private static List<Line> MergeRows(List<Line> lines)
    {
        var result = new List<Line>();
        foreach (var line in lines.OrderBy(l => l.Bounds.Left))
        {
            var b = line.Bounds;
            var target = result.FirstOrDefault(o =>
            {
                var a = o.Bounds;
                int height = Math.Min(a.Height, b.Height);
                int verticalOverlap = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top);
                int gap = b.Left - a.Right;
                return verticalOverlap >= 0.6 * height && gap >= 0 && gap <= 1.2 * height;
            });

            if (target == null)
            {
                result.Add(line);
            }
            else
            {
                target.Words.AddRange(line.Words);
                target.Confidence = Math.Min(target.Confidence, line.Confidence);
            }
        }
        return result;
    }

    /// <summary>
    /// Long names wrap onto several lines ("Primed Bane of / Orokin"), so lines that sit right under each other
    /// and are centered on each other form a block. A block can also hold a mod's description under its name:
    /// that's fine, candidates are made from every run of up to three lines.
    /// </summary>
    private static List<List<Line>> GroupBlocks(List<Line> lines)
    {
        var blocks = new List<List<Line>>();
        foreach (var line in lines.OrderBy(l => l.Bounds.Top))
        {
            var target = blocks.FirstOrDefault(block => Continues(block[^1].Bounds, line.Bounds));
            if (target != null)
                target.Add(line);
            else
                blocks.Add(new List<Line> { line });
        }
        return blocks;
    }

    private static bool Continues(Rectangle prev, Rectangle next)
    {
        int height = Math.Max(prev.Height, next.Height);

        // Directly underneath (allowing slight overlap of descenders/ascenders), not on the same row
        int gap = next.Top - prev.Bottom;
        if (gap > height * 0.9 || gap < -height * 0.25)
            return false;

        int overlap = Math.Min(prev.Right, next.Right) - Math.Max(prev.Left, next.Left);
        int narrower = Math.Min(prev.Width, next.Width);
        return overlap >= narrower * 0.5;
    }

    private static List<NameCandidate> BuildCandidates(List<List<Line>> blocks, Point cursor, int imageWidth)
    {
        var result = new List<NameCandidate>();
        foreach (var block in blocks)
        {
            for (int start = 0; start < block.Count; start++)
            {
                for (int end = start + 1; end <= Math.Min(block.Count, start + 3); end++)
                {
                    var run = block.GetRange(start, end - start);
                    int linesBelow = block.Count - end;

                    AddCandidate(result, run.SelectMany(l => l.Words).ToList(), 0, linesBelow, cursor, imageWidth, null);

                    // A neighbour's label can end up glued to ours on one line ("ed Cleanse Primary Acuity"),
                    // so every part of a single line is a candidate too
                    if (run.Count != 1)
                        continue;
                    var words = run[0].Words;
                    for (int a = 0; a < words.Count; a++)
                    {
                        for (int z = a + 1; z <= words.Count; z++)
                        {
                            if (a == 0 && z == words.Count)
                                continue;
                            // Words of one name sit close together (gap about a third of the letter height);
                            // a neighbour's label glued on by Tesseract is far away. A part that leaves out a
                            // close real word is a piece of a longer name ("Shred" of "Primed Shred"), not a name.
                            if (a > 0 && IsCloseRealWord(words[a - 1], words[a]) || z < words.Count && IsCloseRealWord(words[z], words[z - 1]))
                                continue;
                            int leftover = words.Take(a).Concat(words.Skip(z)).Count(w => IsRealWord(w.Text));
                            AddCandidate(result, words.GetRange(a, z - a), leftover > 0 ? leftover : 0.01, linesBelow, cursor, imageWidth,
                                a > 0 ? words[a - 1].Text : null);
                        }
                    }
                }
            }
        }
        return result;
    }

    private static bool IsRealWord(string text) => text.Count(char.IsLetter) >= 3;

    private static bool IsCloseRealWord(OcrWord word, OcrWord neighbour)
    {
        if (!IsRealWord(word.Text))
            return false;
        int gap = word.Bounds.Left > neighbour.Bounds.Right
            ? word.Bounds.Left - neighbour.Bounds.Right
            : neighbour.Bounds.Left - word.Bounds.Right;
        return gap < Math.Max(word.Bounds.Height, neighbour.Bounds.Height);
    }

    private static void AddCandidate(List<NameCandidate> list, List<OcrWord> words, double leftover, int linesBelow, Point cursor, int imageWidth, string? wordBefore)
    {
        var box = words.Select(w => w.Bounds).Aggregate(Rectangle.Union);

        // Cut by the edge of the capture: probably a neighbour's label with letters missing
        if (box.Left <= 3 || box.Right >= imageWidth - 3)
            return;

        // Median letter height; one badly boxed word shouldn't make the text look bigger than it is
        var heights = words.Select(w => (double)w.Bounds.Height).OrderBy(h => h).ToList();
        double height = heights.Count % 2 == 1
            ? heights[heights.Count / 2]
            : (heights[heights.Count / 2 - 1] + heights[heights.Count / 2]) / 2;
        double dx = cursor.X >= box.Left && cursor.X <= box.Right ? 0 : Math.Min(Math.Abs(box.Left - cursor.X), Math.Abs(box.Right - cursor.X));
        double dy = cursor.Y >= box.Top && cursor.Y <= box.Bottom ? 0 : Math.Min(Math.Abs(box.Top - cursor.Y), Math.Abs(box.Bottom - cursor.Y));

        list.Add(new NameCandidate(
            string.Join(" ", words.Select(w => w.Text)),
            height, dx, dy, box.Bottom < cursor.Y, leftover, linesBelow, wordBefore));
    }

    /// <summary>Whole lines that look like words, ranked the same way as matches but without the catalogue.</summary>
    private static string BestGuess(List<NameCandidate> candidates)
    {
        var lines = candidates.Where(c => !c.IsPartOfLine && CandidateRanking.LooksLikeName(c.Text)).ToList();
        if (lines.Count == 0)
            return "";
        double maxHeight = lines.Max(c => c.Height);
        return lines.OrderBy(c => CandidateRanking.Penalty(c, maxHeight)).First().Text;
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

/// <summary>
/// How likely a candidate is to be the hovered item's name, judging by position and size only (lower is better).
/// Tuned on screenshots of the mod inventory: the hovered card is enlarged, so its name is printed larger,
/// sits in the cursor's column and has the description underneath.
/// </summary>
public static class CandidateRanking
{
    /// <summary>
    /// A label above the cursor with nothing under it is, in an inventory grid, the label of the cell above
    /// (the hovered cell's label is below the cursor). The exception is a hovered mod card: its name is printed
    /// larger than all the text around it, and it may be above the cursor when the cursor rests on the description.
    /// Works on everything read in all passes together, so every copy of a label gets the same verdict.
    /// </summary>
    public static List<NameCandidate> MarkLabelsOfCellAbove(IReadOnlyList<NameCandidate> candidates)
    {
        var heights = candidates.Where(o => !o.IsPartOfLine).Select(o => o.Height).OrderBy(h => h).ToList();
        double typical = heights.Count > 0 ? heights[heights.Count / 2] : 0;
        return candidates.Select(c => c with
        {
            LabelOfCellAbove = c.Above && c.LinesBelow == 0 && c.Dy > LoneAboveMinDy && c.Height < LargeTextRatio * typical
        }).ToList();
    }

    public static double LargeTextRatio = 1.4;

    // Tuning knobs (public so the test harness can try other values)
    public static double HeightWeight = 4, DxScale = 25, DyScale = 150, AbovePenalty = 0.7, AboveDyScale = double.PositiveInfinity,
        LeftoverWeight = 0.6, DescriptionBonus = 0.7, LoneAbovePenalty = 4, LoneAboveMinDy = 40;

    public static double Penalty(NameCandidate c, double maxHeight) =>
        HeightWeight * (1 - c.Height / maxHeight)
        + c.Dx / DxScale
        + c.Dy / DyScale
        + (c.Above ? AbovePenalty + c.Dy / AboveDyScale : 0)
        + (c.LabelOfCellAbove ? LoneAbovePenalty : 0)
        + LeftoverWeight * c.LeftoverWords
        - (c.LinesBelow >= 2 ? DescriptionBonus : 0);

    /// <summary>Letters only, one to five words, at least one of them a real word: "Primed Shred", not "+50% Fire Rate".</summary>
    public static bool LooksLikeName(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length is >= 1 and <= 5
            && words.All(w => w.All(ch => char.IsLetter(ch) || ch is '-' or '\''))
            && words.Any(w => w.Count(char.IsLetter) >= 3);
    }
}
