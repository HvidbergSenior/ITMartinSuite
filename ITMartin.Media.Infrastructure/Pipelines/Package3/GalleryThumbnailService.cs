using ITMartin.Media.Contracts.Contracts.Runtime.Helpers;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Infrastructure.Pipelines.Package3;

public sealed class GalleryThumbnailService : IGalleryThumbnailService
{
    // SmartFolders content used to be skipped on the assumption its entries
    // were symlinks back to an already-thumbnailed original - now that
    // SmartFoldersService always writes real copies (see ISmartFoldersService
    // docs), that assumption no longer holds, so those files need their own
    // thumbnails generated too or a video inside a Person/Trip folder never
    // gets one at all.
    private static readonly HashSet<string> SkippedFolders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "@eadir", "#recycle", "#snapshot", ".@__thumb",
            "@recently-snapshot", ".synophoto", ".package1", ".package2", ".package3",
            "thumbnails", "working", "enhanced", "manifests", "temp",
            "_Galleri", LibraryPolishService.UnplayableFolderName,
        };

    private readonly IThumbnailService _thumbnailService;
    private readonly ILogger<GalleryThumbnailService> _logger;

    public GalleryThumbnailService(IThumbnailService thumbnailService, ILogger<GalleryThumbnailService> logger)
    {
        _thumbnailService = thumbnailService;
        _logger = logger;
    }

    public async Task<int> GenerateAsync(string libraryPath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(libraryPath)) return 0;

        var images = EnumerateImages(libraryPath).ToList();
        var generated = 0;
        var processed = 0;

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount),
            CancellationToken = cancellationToken,
        };

        await Parallel.ForEachAsync(images, parallelOptions, async (file, ct) =>
        {
            var thumbDir = Path.Combine(Path.GetDirectoryName(file)!, "thumbnails");
            var thumbPath = Path.Combine(thumbDir, Path.GetFileNameWithoutExtension(file) + ".jpg");

            var done = Interlocked.Increment(ref processed);
            if (done % 500 == 0)
                _logger.LogInformation("Gallery thumbnail progress: {Done}/{Total}", done, images.Count);

            if (File.Exists(thumbPath)) return;

            try
            {
                Directory.CreateDirectory(thumbDir);
                await _thumbnailService.GenerateAsync(file, thumbPath, ct);
                Interlocked.Increment(ref generated);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gallery thumbnail generation failed for {File}", file);
            }
        });

        _logger.LogInformation(
            "Gallery thumbnail generation complete for {LibraryPath}: {Total} images, {Generated} new thumbnails",
            libraryPath, images.Count, generated);

        return generated;
    }

    // Previously images only - videos never got a thumbnails/ entry at all,
    // so gallery-web's video cards (and any folder cover that happened to
    // land on a video-only folder) always fell back to a generic play-icon
    // placeholder instead of a real frame. ThumbnailService.GenerateAsync
    // already supports video (ffmpeg frame grab) - just wasn't being asked to.
    private static IEnumerable<string> EnumerateImages(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            if (MediaTypeHelper.IsImage(file) || MediaTypeHelper.IsVideo(file))
                yield return file;
        }

        foreach (var subDir in Directory.EnumerateDirectories(directory))
        {
            var name = Path.GetFileName(subDir);
            if (SkippedFolders.Contains(name) || name.StartsWith('@') || name.StartsWith('#') || name.StartsWith('.'))
                continue;

            foreach (var file in EnumerateImages(subDir))
                yield return file;
        }
    }
}
