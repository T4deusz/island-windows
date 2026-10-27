using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Media.Ocr;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Island;
internal static class CaptureTools
{
    internal static void ConvertImage(string source, string target, string type)
    {
        using var input = File.OpenRead(source); var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad); SaveImage(decoder.Frames[0], target, type);
    }
    internal static async Task ConvertImageAsync(string source, string target, string type)
    {
        BitmapSource bitmap;
        if (Path.GetExtension(source).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            var document = await PdfDocument.LoadFromFileAsync(await StorageFile.GetFileFromPathAsync(Path.GetFullPath(source))); if (document.PageCount == 0) throw new IOException("This PDF has no pages.");
            using var page = document.GetPage(0); using var stream = new InMemoryRandomAccessStream(); await page.RenderToStreamAsync(stream, new PdfPageRenderOptions { DestinationWidth = 1800 });
            using var input = stream.AsStreamForRead(); var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = input; image.EndInit(); image.Freeze(); bitmap = image;
        }
        else { using var input = File.OpenRead(source); bitmap = System.Windows.Media.Imaging.BitmapDecoder.Create(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0]; }
        if ((long)bitmap.PixelWidth * bitmap.PixelHeight > 40_000_000) throw new IOException("Image exceeds the 40-megapixel conversion limit.");
        if (type == "PDF") SavePdf(bitmap, target); else SaveImage(bitmap, target, type);
    }
    internal static void SavePdf(BitmapSource bitmap, string target)
    {
        var jpeg = new JpegBitmapEncoder { QualityLevel = 94 }; jpeg.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap)); using var image = new MemoryStream(); jpeg.Save(image); byte[] bytes = image.ToArray();
        using var output = new MemoryStream(); var offsets = new System.Collections.Generic.List<long> { 0 };
        void Write(string value) { byte[] data = System.Text.Encoding.ASCII.GetBytes(value); output.Write(data); }
        void Object(int id, string body) { offsets.Add(output.Position); Write($"{id} 0 obj\n{body}\nendobj\n"); }
        Write("%PDF-1.4\n"); Object(1, "<< /Type /Catalog /Pages 2 0 R >>"); Object(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        int width = bitmap.PixelWidth, height = bitmap.PixelHeight; Object(3, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {width} {height}] /Resources << /XObject << /Image0 4 0 R >> >> /Contents 5 0 R >>");
        offsets.Add(output.Position); Write($"4 0 obj\n<< /Type /XObject /Subtype /Image /Width {width} /Height {height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {bytes.Length} >>\nstream\n"); output.Write(bytes); Write("\nendstream\nendobj\n");
        string paint = $"q {width} 0 0 {height} 0 0 cm /Image0 Do Q"; Object(5, $"<< /Length {paint.Length} >>\nstream\n{paint}\nendstream");
        long xref = output.Position; Write("xref\n0 6\n0000000000 65535 f \n"); for (int i = 1; i < offsets.Count; i++) Write(offsets[i].ToString("D10", System.Globalization.CultureInfo.InvariantCulture) + " 00000 n \n"); Write($"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        WriteFile(target, stream => { output.Position = 0; output.CopyTo(stream); });
    }
    internal static void SaveImage(BitmapSource source, string target, string type)
    {
        System.Windows.Media.Imaging.BitmapEncoder encoder = type switch { "JPEG" => new JpegBitmapEncoder { QualityLevel = 94 }, "TIFF" => new TiffBitmapEncoder(), "BMP" => new BmpBitmapEncoder(), _ => new PngBitmapEncoder() };
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(source)); WriteFile(target, encoder.Save);
    }
    private static void WriteFile(string target, Action<Stream> write)
    {
        string temporary = target + ".island-" + Guid.NewGuid().ToString("N") + ".tmp";
        try { using (var output = File.Create(temporary)) write(output); File.Move(temporary, target, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    internal static async Task<string> Ocr(BitmapSource bitmap)
    {
        var engine = OcrEngine.TryCreateFromUserProfileLanguages() ?? throw new InvalidOperationException("Install a Windows OCR language pack first.");
        double scale = Math.Min(1, Math.Min(OcrEngine.MaxImageDimension / (double)bitmap.PixelWidth, OcrEngine.MaxImageDimension / (double)bitmap.PixelHeight));
        if (scale < 1) bitmap = new TransformedBitmap(bitmap, new ScaleTransform(scale, scale));
        var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0); int stride = converted.PixelWidth * 4; byte[] bytes = new byte[stride * converted.PixelHeight]; converted.CopyPixels(bytes, stride, 0);
        using var software = new SoftwareBitmap(BitmapPixelFormat.Bgra8, converted.PixelWidth, converted.PixelHeight, BitmapAlphaMode.Premultiplied); software.CopyFromBuffer(bytes.AsBuffer());
        var result = await engine.RecognizeAsync(software); return result.Text;
    }
}
internal sealed class CameraMirror : IDisposable
{
    private readonly Image target;
    private MediaCapture? capture;
    private MediaFrameReader? reader;
    private int framePending;
    private volatile bool disposed;
    private byte[] pixels = Array.Empty<byte>();
    private WriteableBitmap? bitmap;
    internal CameraMirror(Image image) { target = image; }
    internal async Task Start()
    {
        capture = new MediaCapture();
        await capture.InitializeAsync(new MediaCaptureInitializationSettings { StreamingCaptureMode = StreamingCaptureMode.Video, MemoryPreference = MediaCaptureMemoryPreference.Cpu, SharingMode = MediaCaptureSharingMode.SharedReadOnly });
        if (disposed) return;
        var source = capture.FrameSources.Values.FirstOrDefault(f => f.Info.SourceKind == MediaFrameSourceKind.Color) ?? throw new InvalidOperationException("No compatible color camera is available.");
        reader = await capture.CreateFrameReaderAsync(source, MediaEncodingSubtypes.Bgra8); if (disposed) { reader.Dispose(); reader = null; return; }
        reader.FrameArrived += Frame; var status = await reader.StartAsync(); if (status != MediaFrameReaderStartStatus.Success) throw new InvalidOperationException("The camera could not start: " + status);
    }
    private async void Frame(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        if (disposed || Interlocked.Exchange(ref framePending, 1) != 0) return;
        try
        {
            using var frame = sender.TryAcquireLatestFrame(); var software = frame?.VideoMediaFrame?.SoftwareBitmap; if (software == null) return;
            using var converted = SoftwareBitmap.Convert(software, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied); int width = converted.PixelWidth, height = converted.PixelHeight;
            int length = checked(width * height * 4); if (pixels.Length != length) pixels = new byte[length]; converted.CopyToBuffer(pixels.AsBuffer());
            await target.Dispatcher.InvokeAsync(() =>
            {
                if (disposed) return; if (bitmap == null || bitmap.PixelWidth != width || bitmap.PixelHeight != height) { bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null); target.Source = bitmap; }
                bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), pixels, width * 4, 0);
            });
        }
        catch (Exception ex) { Log.Write(ex); }
        finally { Interlocked.Exchange(ref framePending, 0); }
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true; if (reader != null) { reader.FrameArrived -= Frame; reader.Dispose(); reader = null; } capture?.Dispose(); capture = null;
    }
}
