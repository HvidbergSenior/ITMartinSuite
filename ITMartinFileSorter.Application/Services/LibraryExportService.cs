using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Enums;
using ITMartinFileSorter.Domain.Interfaces;
using ITMartinFileSorter.Domain.Helpers;
namespace ITMartinFileSorter.Application.Services;

public class LibraryExportService
{
    private readonly IVideoBatchService _videoService;
    private readonly IImageBatchService _imageService;
    
    public LibraryExportService(
        IVideoBatchService videoService,
        IImageBatchService imageService)
    {
        _videoService = videoService;
        _imageService = imageService;
    }

    public async Task ExportAsync(
    IEnumerable<MediaFile> files,
    string root,
    Func<int, int, string, string, Task>? progress)
{
    var list = files?.ToList() ?? new List<MediaFile>();

    if (!list.Any())
        return;

    if (string.IsNullOrWhiteSpace(root))
        throw new Exception("Export root is invalid");

    EnsureBaseFolders(root);

    int total = list.Count;
    int done = 0;

    // =========================
    // 📦 STEP 1: COPY FILES
    // =========================
    foreach (var file in list)
    {
        try
        {
            var category = CategoryHelper.GetCategory(file);

            var monthName = new DateTime(file.Year, file.Month, 1)
                .ToString("MMMM");

            var targetDir = Path.Combine(root, category, file.Year.ToString(), monthName);
            Directory.CreateDirectory(targetDir);

            var targetPath = Path.Combine(targetDir, file.FileName);

            if (!File.Exists(targetPath))
            {
                await CopyFileAsync(file.FullPath, targetPath);
            }

// ✅ STORE EXPORTED PATH
            file.ExportedPath = targetPath;
            done++;

            if (progress != null)
                await progress(done, total, file.FileName, "Copying files...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"🔥 EXPORT ERROR: {file.FullPath}");
            Console.WriteLine(ex);
        }
    }

    // =========================
    // 🖼 STEP 2: CONVERT IMAGES
    // =========================
    if (progress != null)
        await progress(0, 0, "", "Converting images...");

    await _imageService.ConvertAllImagesAsync(
        list,
        (d, t, fileName) =>
        {
            progress?.Invoke(d, t, fileName, "Converting images...");
        });
    // =========================
    // 🎬 STEP 3: CONVERT VIDEOS
    // =========================
    if (progress != null)
        await progress(0, 0, "", "Converting videos...");

    await _videoService.ConvertAllVideosAsync(
        list,
        (d, t, fileName) =>
        {
            progress?.Invoke(d, t, fileName, "Converting videos...");
        });
    // =========================
    // ✅ DONE
    // =========================
    if (progress != null)
        await progress(total, total, "", "Done ✅");
}

    void EnsureBaseFolders(string exportRoot)
    {
        var baseFolders = new[]
        {
            "Images",
            "Videos",
            "Documents",
            "Audio",
            "Memes",
            "Screenshots"
        };

        foreach (var folder in baseFolders)
        {
            var path = Path.Combine(exportRoot, folder);
            Directory.CreateDirectory(path);
        }
    }

    async Task CopyFileAsync(string sourcePath, string destinationPath)
    {
        const int bufferSize = 81920;

        using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize,
            useAsync: true);

        using var destination = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize,
            useAsync: true);

        await source.CopyToAsync(destination);
    }
}