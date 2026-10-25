using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace ITMartinLaes.Infrastructure.External;

/// <summary>The PDFs in the books folder (Laes__DataDir/books), each under its own random name.</summary>
public sealed class FileBookStore(string dir) : IBookFiles
{
    public async Task<string> SaveAsync(Stream pdf, CancellationToken ct = default)
    {
        Directory.CreateDirectory(dir);
        var name = $"{Guid.NewGuid():N}.pdf";
        await using var file = File.Create(Path.Combine(dir, name));
        await pdf.CopyToAsync(file, ct);
        return name;
    }

    public string? PathOf(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains("..")) return null;
        var path = Path.Combine(dir, fileName);
        return File.Exists(path) ? path : null;
    }

    public void Delete(string fileName)
    {
        if (PathOf(fileName) is { } path) File.Delete(path);
        var pages = PagesDir(dir, fileName);
        if (Directory.Exists(pages)) Directory.Delete(pages, recursive: true);
    }

    /// <summary>Where a book's page pictures are kept: books/{name}-sider/.</summary>
    public static string PagesDir(string dir, string fileName) => Path.Combine(dir, Path.GetFileNameWithoutExtension(fileName) + "-sider");
}

/// <summary>Pages as JPEG pictures with poppler's pdftoppm (in the container: apt poppler-utils). One page at a time, made
/// the first time it is asked for and kept; a few at once at most, so one fast reader cannot load the server.</summary>
public sealed class PopplerPageImages(string dir, ILogger<PopplerPageImages> log) : IPageImages
{
    private static readonly SemaphoreSlim Gate = new(3, 3);

    public async Task<string?> PageAsync(string fileName, int page, int width, CancellationToken ct = default)
    {
        var pdf = Path.Combine(dir, fileName);
        if (fileName.Contains('/') || fileName.Contains('\\') || !File.Exists(pdf)) return null;
        var pages = FileBookStore.PagesDir(dir, fileName);
        var jpg = Path.Combine(pages, $"{page}-{width}.jpg");
        if (File.Exists(jpg)) return jpg;
        await Gate.WaitAsync(ct);
        try
        {
            if (File.Exists(jpg)) return jpg;
            Directory.CreateDirectory(pages);
            var prefix = Path.Combine(pages, $"tmp-{page}-{width}-{Guid.NewGuid():N}");
            var psi = new System.Diagnostics.ProcessStartInfo("pdftoppm") { RedirectStandardError = true, UseShellExecute = false };
            foreach (var a in new[] { "-f", $"{page}", "-l", $"{page}", "-jpeg", "-jpegopt", "quality=82", "-scale-to-x", $"{width}",
                         "-scale-to-y", "-1", "-singlefile", pdf, prefix })
                psi.ArgumentList.Add(a);
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc is null) return null;
            var err = proc.StandardError.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);
            if (proc.ExitCode != 0 || !File.Exists(prefix + ".jpg"))
            {
                log.LogWarning("Page {Page} of {File} could not be made: {Error}", page, fileName, await err);
                return null;
            }
            File.Move(prefix + ".jpg", jpg, overwrite: true);
            return jpg;
        }
        catch (System.ComponentModel.Win32Exception) { log.LogWarning("pdftoppm is not installed"); return null; }
        finally { Gate.Release(); }
    }
}

/// <summary>The PDF's own text layer, page by page, in reading order (PdfPig). A scanned PDF without text gives empty pages.</summary>
public sealed class PdfPigText : IPdfText
{
    public int PageCount(string path)
    {
        try
        {
            using var doc = PdfDocument.Open(path);
            return doc.NumberOfPages;
        }
        catch (Exception) { return 0; }
    }

    public List<string> Pages(string path, int from, int to)
    {
        using var doc = PdfDocument.Open(path);
        var list = new List<string>();
        for (var n = Math.Max(1, from); n <= Math.Min(to, doc.NumberOfPages); n++)
        {
            try { list.Add(ContentOrderTextExtractor.GetText(doc.GetPage(n)).Trim()); }
            catch (Exception) { list.Add(""); }
        }
        return list;
    }
}
