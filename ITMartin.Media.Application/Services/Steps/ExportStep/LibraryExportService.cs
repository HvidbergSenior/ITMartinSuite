using ITMartin.Media.Application.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Helpers;
using System.Linq;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Services.Steps.ExportStep;

public class LibraryExportService
    : ILibraryExportService
{
    private readonly IMediaNamingService
        _mediaNamingService;
    private readonly IAudioMetadataService
        _audioMetadataService;
    private readonly ILogger<LibraryExportService>
        _logger;
    public LibraryExportService(
        IMediaNamingService mediaNamingService, IAudioMetadataService audioMetadataService, ILogger<LibraryExportService> logger)
    {
        _mediaNamingService =
            mediaNamingService;
        _audioMetadataService =
            audioMetadataService;
        _logger = logger;
    }

    public async Task ExportAsync(
        IEnumerable<MediaFile> files,
        string root,
        Func<int, int, string, string, Task>? progress)
    {
        var list =
            files?.ToList() ?? [];

        if (!list.Any())
            return;

        if (string.IsNullOrWhiteSpace(root))
        {
            throw new Exception(
                "Export root is invalid");
        }

        EnsureBaseFolders(root);

        int total = list.Count;
        int done = 0;

        // =========================
        // COPY FILES
        // =========================

        foreach (var file in list)
        {
            try
            {
                var category =
                    CategoryHelper
                        .GetCategory(file);

                // Music has no meaningful "date taken" the way photos do (ID3
                // Year is release year, not acquisition date), so it always
                // gets an Artist/Album folder instead of being routed through
                // the year/month or Undated logic below.
                var isMusic =
                    category == "Musik";

                var musicSubPath =
                    isMusic
                        ? Path.Combine(
                            SanitizeFolderName(
                                string.IsNullOrWhiteSpace(file.Artist) ? "Ukendt kunstner" : file.Artist),
                            SanitizeFolderName(
                                string.IsNullOrWhiteSpace(file.Album) ? "Ukendt album" : file.Album))
                        : null;

                // SAFER DATE HANDLING

                var safeMonth =
                    Math.Clamp(
                        file.Month,
                        1,
                        12);

                var safeYear =
                    Math.Max(
                        file.Year,
                        2000);

                var monthFolder =
                    $"{safeMonth:00}-{new DateTime(
                        safeYear,
                        safeMonth,
                        1).ToString("MMMM")}";

                var targetDir =
                    file.ExportSubFolder == "Duplicates"
                        ? musicSubPath is not null
                            ? Path.Combine(root, "Duplicates", category, musicSubPath)
                            : Path.Combine(
                                root,
                                "Duplicates",
                                category,
                                safeYear.ToString(),
                                monthFolder)
                        : file.ExportSubFolder == "DeleteCandidates"
                            ? Path.Combine(
                                root,
                                "DeleteCandidates",
                                category)
                            : musicSubPath is not null
                                ? Path.Combine(root, category, musicSubPath)
                                : file.IsYearOnly
                                    // Year came from an ancestor folder name, not a
                                    // real date - sort by it, but never claim a
                                    // specific month we don't actually know.
                                    ? Path.Combine(
                                        root,
                                        category,
                                        safeYear.ToString(),
                                        "Ukendt måned")
                                    : !file.IsDateReliable
                                        ? Path.Combine(
                                            root,
                                            "Undated",
                                            category)
                                        : Path.Combine(
                                            root,
                                            category,
                                            safeYear.ToString(),
                                            monthFolder);

                Directory.CreateDirectory(
                    targetDir);

                // =========================
                // SOURCE FILE
                // =========================

                var sourcePath =
                    file.NormalizedPath ??
                    file.FullPath;
                _logger.LogInformation(
                    """
                    Export:
                    Original={Original}
                    Normalized={Normalized}
                    Using={Using}
                    """,
                    file.FullPath,
                    file.NormalizedPath,
                    file.NormalizedPath ?? file.FullPath);
                // =========================
                // AI FILE NAME
                // =========================

                var safeFileName =
                    isMusic && file.TrackNumber is > 0 && !string.IsNullOrWhiteSpace(file.Title)
                        ? $"{file.TrackNumber:00} - {SanitizeFolderName(file.Title!)}{Path.GetExtension(file.FullPath).ToLowerInvariant()}"
                        : _mediaNamingService
                            .BuildFileName(file);

                var targetPath =
                    Path.Combine(
                        targetDir,
                        safeFileName);

                // Avoid collisions

                targetPath =
                    EnsureUniqueFileName(
                        targetPath);

                // =========================
                // COPY
                // =========================

                if (!File.Exists(targetPath))
                {
                    await CopyFileAsync(
                        sourcePath,
                        targetPath);
                }

                // One cover.jpg per album folder, pulled from whichever track
                // happens to carry embedded artwork - not every track in an
                // album has it, so this isn't limited to the first file copied.
                if (isMusic && file.ExportSubFolder is not ("Duplicates" or "DeleteCandidates"))
                {
                    var coverPath =
                        Path.Combine(targetDir, "cover.jpg");

                    if (!File.Exists(coverPath))
                    {
                        var coverBytes =
                            _audioMetadataService.GetCoverArt(sourcePath);

                        if (coverBytes is { Length: > 0 })
                        {
                            await File.WriteAllBytesAsync(
                                coverPath,
                                coverBytes);
                        }
                    }
                }

                // =========================
                // STORE EXPORTED PATH
                // =========================

                file.ExportedPath =
                    targetPath;

                done++;

                if (progress != null)
                {
                    await progress(
                        done,
                        total,
                        Path.GetFileName(targetPath),
                        "Copying files...");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"🔥 EXPORT ERROR: {file.FullPath}");

                Console.WriteLine(ex);
            }
        }

        // =========================
        // DONE
        // =========================

        if (progress != null)
        {
            await progress(
                total,
                total,
                "",
                "Done ✅");
        }
    }

    private static string SanitizeFolderName(
        string name)
    {
        var invalid =
            Path.GetInvalidFileNameChars();

        var cleaned =
            new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray())
                .Trim()
                .TrimEnd('.');

        return string.IsNullOrWhiteSpace(cleaned) ? "Ukendt" : cleaned;
    }

    private static void EnsureBaseFolders(
        string exportRoot)
    {
        var baseFolders =
            new[]
            {
                "Images",
                "Videos",
                "Documents",
                "Musik",
                "Memes",
                "Screenshots",
                "LivePhotos",
                "DeleteCandidates",
                "Duplicates",
                "Undated",
                "Unhandled"
            };

        foreach (var folder in baseFolders)
        {
            var path =
                Path.Combine(
                    exportRoot,
                    folder);

            Directory.CreateDirectory(path);
        }
    }

    private static async Task CopyFileAsync(
        string sourcePath,
        string destinationPath)
    {
        const int bufferSize = 81920;

        using var source =
            new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize,
                useAsync: true);

        using var destination =
            new FileStream(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize,
                useAsync: true);

        await source.CopyToAsync(
            destination);
    }

    // Collision handling is filename-only, not content-based - if two source
    // files share a name (e.g. the same photo/video re-imported from two
    // different backups or an add-on's corrected copy sitting next to the
    // original) this keeps both as separate files (name, name_2, name_3, ...)
    // instead of deduping. Real, byte-identical duplicates can end up in the
    // exported library this way, inflating file/photo/video counts - see
    // DuplicateDetectionWorkflowStep for the actual dedup pass, which runs
    // earlier and is a separate concern from this pure naming-collision fix.
    private static string EnsureUniqueFileName(
        string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var directory =
            Path.GetDirectoryName(path)!;

        var name =
            Path.GetFileNameWithoutExtension(path);

        var ext =
            Path.GetExtension(path);

        var counter = 2;

        while (true)
        {
            var candidate =
                Path.Combine(
                    directory,
                    $"{name}_{counter}{ext}");

            if (!File.Exists(candidate))
            {
                return candidate;
            }

            counter++;
        }
    }
}