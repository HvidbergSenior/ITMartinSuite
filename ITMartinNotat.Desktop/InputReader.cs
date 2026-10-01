using System.IO;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;

namespace ITMartinNotat;

/// <summary>Reads her keyword files: Word, Notepad text, or a photo/scan (Windows' own OCR, runs on the PC).</summary>
public static class InputReader
{
    public static readonly string[] ImageExt = [".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff"];

    public static async Task<string> ReadAsync(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".docx") return ReadDocx(path);
        if (ext is ".txt" or ".text" or ".md") return await File.ReadAllTextAsync(path);
        if (ImageExt.Contains(ext)) return await OcrAsync(path);
        throw new NotSupportedException($"Filtypen {ext} kan ikke læses. Brug Word (.docx), tekst (.txt) eller et billede.");
    }

    private static string ReadDocx(string path)
    {
        using var doc = WordprocessingDocument.Open(path, false);
        var body = doc.MainDocumentPart?.Document.Body;
        if (body is null) return "";
        var sb = new StringBuilder();
        foreach (var p in body.Descendants<Paragraph>())
            sb.AppendLine(p.InnerText);
        return sb.ToString().Trim();
    }

    private static async Task<string> OcrAsync(string path)
    {
        var engine = OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("da"))
                     ?? OcrEngine.TryCreateFromUserProfileLanguages()
                     ?? throw new InvalidOperationException("Windows' tekstgenkendelse (OCR) er ikke installeret på denne PC.");
        var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(path));
        using var stream = await file.OpenAsync(FileAccessMode.Read);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        using var bitmap = await decoder.GetSoftwareBitmapAsync();
        var result = await engine.RecognizeAsync(bitmap);
        return string.Join(Environment.NewLine, result.Lines.Select(l => l.Text));
    }
}
