using ITMartinFileSorter.Domain.Entities;

namespace ITMartinFileSorter.Application.Services;

public class LibraryExportService
{
    private readonly LibraryPathService _pathService;
    private readonly FastVideoBatchExportService _videoService;
    private readonly ImageBatchExportService _imageService;

    public LibraryExportService(
        LibraryPathService pathService,
        FastVideoBatchExportService videoService,
        ImageBatchExportService imageService)
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
        var grouped = fileList
            .GroupBy(f => _pathService.BuildFolderPath(f))
            .ToList();

        int total = fileList.Count;
        int done = 0;

        foreach (var group in grouped)
        {
            var targetFolder = Path.Combine(exportRoot, group.Key);

            Directory.CreateDirectory(targetFolder);

            int index = 1;

            foreach (var file in group)
            {
                var newName = _pathService.BuildFileName(file, index);

                var destination = Path.Combine(targetFolder, newName);
                var folderPath = _pathService.BuildFolderPath(file);

                Console.WriteLine("===== EXPORT DEBUG =====");
                Console.WriteLine($"File: {file.FileName}");
                Console.WriteLine($"ComputedPath: {folderPath}");
                Console.WriteLine("========================");
                File.Copy(file.FullPath, destination, true);
                Console.WriteLine($"EXTENSION: {file.Extension}");
                index++;
                done++;

                if (onProgress != null)
                {
                    await onProgress(
                        done,
                        total,
                        newName,
                        "Copying files...");
                }
            }
        }

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
}