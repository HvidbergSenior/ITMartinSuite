using ITMartin.Media.Application.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Helpers;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Services.Steps.ExportStep;

public class LibraryExportService
    : ILibraryExportService
{
    private readonly IMediaNamingService
        _mediaNamingService;
    private readonly ILogger<LibraryExportService>
        _logger;
    public LibraryExportService(
        IMediaNamingService mediaNamingService, ILogger<LibraryExportService> logger)
    {
        _mediaNamingService =
            mediaNamingService;
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
                        ? Path.Combine(
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
                    _mediaNamingService
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

    private static void EnsureBaseFolders(
        string exportRoot)
    {
        var baseFolders =
            new[]
            {
                "Images",
                "Videos",
                "Documents",
                "Audio",
                "Memes",
                "Screenshots",
                "LivePhotos",
                "DeleteCandidates",
                "Duplicates",
                "Undated"
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