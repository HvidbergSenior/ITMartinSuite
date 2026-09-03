using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;

namespace ITMartinFileSorter.Server.Controllers;

[ApiController]
[Route("api/guest-export")]
public class GuestExportController : ControllerBase
{
    private static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".heic", ".avif",
        ".mp4", ".mov", ".mkv", ".avi", ".m4v", ".webm"
    };

    private static readonly HashSet<string> AlreadyCompressed = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".mkv", ".avi", ".m4v", ".webm",
        ".jpg", ".jpeg", ".heic", ".avif", ".webp"
    };

    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public GuestExportController(IConfiguration config, IWebHostEnvironment env)
    {
        _config = config;
        _env = env;
    }

    [HttpGet("folder")]
    public async Task ExportFolderAsync([FromQuery] string path, CancellationToken ct)
    {
        var libraryRoot = _config["MediaSettings:LibraryRoot"] ?? "";
        var sourceRoot  = _config["MediaSettings:SourceRoot"]  ?? "";

        if (string.IsNullOrWhiteSpace(path)
            || !Directory.Exists(path)
            || (!PathSecurity.IsUnder(path, libraryRoot) && !PathSecurity.IsUnder(path, sourceRoot)))
        {
            Response.StatusCode = 404;
            return;
        }

        var folderName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var zipName    = $"{folderName}_viewer.zip";

        Response.ContentType = "application/zip";
        Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{zipName}\"");

        using var archive = new ZipArchive(Response.Body, ZipArchiveMode.Create, leaveOpen: true);

        // Bundle the self-contained viewer
        var viewerHtmlPath = Path.Combine(_env.WebRootPath, "viewer.html");
        if (System.IO.File.Exists(viewerHtmlPath))
        {
            var viewerEntry = archive.CreateEntry("viewer.html", CompressionLevel.SmallestSize);
            await using var es = viewerEntry.Open();
            await using var vs = System.IO.File.OpenRead(viewerHtmlPath);
            await vs.CopyToAsync(es, ct);
        }

        // Add media files, skip re-compressing already-compressed formats
        foreach (var file in Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories))
        {
            if (ct.IsCancellationRequested) break;

            var ext = Path.GetExtension(file);
            if (!MediaExtensions.Contains(ext)) continue;

            var relative = Path.GetRelativePath(path, file).Replace('\\', '/');
            var level    = AlreadyCompressed.Contains(ext)
                ? CompressionLevel.NoCompression
                : CompressionLevel.SmallestSize;

            var entry = archive.CreateEntry(relative, level);
            await using var es = entry.Open();
            await using var fs = System.IO.File.OpenRead(file);
            await fs.CopyToAsync(es, ct);
        }
    }

}
