using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Interfaces;

namespace ITMartinFileSorter.Application.Services;

public class LibraryExportService
{
    private readonly LibraryPathService _pathService;
    private readonly IVideoBatchService _videoService;
    private readonly IImageBatchService _imageService;
    
    public LibraryExportService(
        LibraryPathService pathService,
        IVideoBatchService videoService,
        IImageBatchService imageService)
    {
        _pathService = pathService;
        _videoService = videoService;
        _imageService = imageService;
    }

    public async Task ExportAsync(
        IEnumerable<MediaFile> files,
        string exportRoot,
        Func<int, int, string, string, Task>? onProgress = null)
    {
        var fileList = files.ToList();

        if (!fileList.Any())
            return;

        EnsureBaseFolders(exportRoot);

        var grouped = fileList
            .GroupBy(f => _pathService.BuildFolderPath(f))
            .ToList();

        int total = fileList.Count;
        int done = 0;

        // 🔥 INITIAL PROGRESS (forces UI to show)
        if (onProgress != null)
            await onProgress(0, total, "", "Starting export...");

        foreach (var group in grouped)
        {
            var targetFolder = Path.Combine(exportRoot, group.Key);
            Directory.CreateDirectory(targetFolder);

            int index = 1;

            foreach (var file in group)
            {
                var newName = _pathService.BuildFileName(file, index);
                var destination = Path.Combine(targetFolder, newName);

                // 🔥 BEFORE COPY
                if (onProgress != null)
                {
                    await onProgress(done, total, newName, "Copying files...");
                }

                // ✅ ASYNC COPY
                await CopyFileAsync(file.FullPath, destination);

                index++;
                done++;

                // 🔥 AFTER COPY
                if (onProgress != null)
                {
                    await onProgress(done, total, newName, "Copying files...");
                }

                // 🔥 ALLOW UI TO UPDATE
                await Task.Yield();
            }
        }

        // ===== VIDEO CONVERSION =====
        await _videoService.ConvertAllVideosAsync(
            exportRoot,
            async (doneCount, totalCount, fileName) =>
            {
                if (onProgress != null)
                {
                    await onProgress(
                        doneCount,
                        totalCount,
                        fileName,
                        "Converting videos...");
                }
            });

        // ===== IMAGE CONVERSION =====
        await _imageService.ConvertAllImagesAsync(
            exportRoot,
            async (doneCount, totalCount, fileName) =>
            {
                if (onProgress != null)
                {
                    await onProgress(
                        doneCount,
                        totalCount,
                        fileName,
                        "Converting images...");
                }
            });
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