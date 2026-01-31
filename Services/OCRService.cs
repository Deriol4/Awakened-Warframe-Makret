using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Tesseract;

namespace AwakenedWarframeMarket.Services;

public class OCRService : IDisposable
{
    private TesseractEngine? _engine;
    private readonly string _tessdataPath;
    private bool _disposed;

    public OCRService()
    {
        _tessdataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
    }

    public async Task InitializeAsync()
    {
        await EnsureTessdataExistsAsync();
        _engine = new TesseractEngine(_tessdataPath, "eng", EngineMode.Default);
        _engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 '-");
    }

    private async Task EnsureTessdataExistsAsync()
    {
        Directory.CreateDirectory(_tessdataPath);
        var trainedDataPath = Path.Combine(_tessdataPath, "eng.traineddata");

        if (!File.Exists(trainedDataPath))
        {
            System.Diagnostics.Debug.WriteLine("Downloading eng.traineddata...");

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(5);

            var url = "https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata";
            var data = await client.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(trainedDataPath, data);

            System.Diagnostics.Debug.WriteLine("eng.traineddata downloaded successfully");
        }
    }

    public string RecognizeItemName(Bitmap image)
    {
        if (_engine == null)
            throw new InvalidOperationException("OCR engine not initialized. Call InitializeAsync first.");

        using var processed = PreprocessImage(image);
        using var pix = BitmapToPix(processed);
        using var page = _engine.Process(pix);

        return ExtractCenterText(page, image.Width, image.Height);
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

    private string ExtractCenterText(Page page, int imageWidth, int imageHeight)
    {
        using var iter = page.GetIterator();

        string bestMatch = "";
        int centerX = imageWidth / 2;
        int centerY = imageHeight / 2;
        double minDistance = double.MaxValue;

        iter.Begin();
        do
        {
            if (iter.TryGetBoundingBox(PageIteratorLevel.TextLine, out var bounds))
            {
                var text = iter.GetText(PageIteratorLevel.TextLine)?.Trim() ?? "";

                // Filter: skip short text and pure numbers
                if (text.Length < 4 || int.TryParse(text, out _))
                    continue;

                // Skip text that looks like prices (e.g., "35 platinum", "100p")
                if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^\d+\s*(p|plat|platinum)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    continue;

                // Calculate distance to center
                int textCenterX = bounds.X1 + (bounds.X2 - bounds.X1) / 2;
                int textCenterY = bounds.Y1 + (bounds.Y2 - bounds.Y1) / 2;

                double distance = Math.Sqrt(
                    Math.Pow(textCenterX - centerX, 2) +
                    Math.Pow(textCenterY - centerY, 2)
                );

                if (distance < minDistance)
                {
                    minDistance = distance;
                    bestMatch = CleanItemName(text);
                }
            }
        } while (iter.Next(PageIteratorLevel.TextLine));

        return bestMatch;
    }

    private string CleanItemName(string text)
    {
        // Remove common OCR artifacts
        text = text.Trim();
        text = System.Text.RegularExpressions.Regex.Replace(text, @"[|\\\/\[\]{}]", "");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        return text;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _engine?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
