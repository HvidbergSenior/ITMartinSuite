using ITMartin.Media.Application.Pipelines.AnalogDigitize.Services;
using ITMartin.Media.Application.Pipelines.Package4.Services;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Helpers;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests.Package4;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package4.Orchestration;

public sealed class Package4WorkflowOrchestrator
{
    private readonly Package4WorkflowFactory _factory;
    private readonly QuickSortManifestLoader _manifestLoader;
    private readonly ILogger<Package4WorkflowOrchestrator> _logger;

    public Package4WorkflowOrchestrator(
        Package4WorkflowFactory factory,
        QuickSortManifestLoader manifestLoader,
        ILogger<Package4WorkflowOrchestrator> logger)
    {
        _factory = factory;
        _manifestLoader = manifestLoader;
        _logger = logger;
    }

    public async Task<Package4WorkflowStartResult> StartAsync(StartPackage4Request request, CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(request.SourceLibraryPath, "manifest.json");
        var hasRunThroughQuickSort = File.Exists(manifestPath);

        // Package4 is meant to run on clips that already went through
        // QuickSort (manifest.json is QuickSort's own output marker) - the raw
        // folder scan below exists for the "one-off clip, never sorted"
        // workflow and still works, but is a degraded/unverified path (no
        // real category/date metadata, no dedup, no rotation-fix), so it's
        // worth a loud warning rather than silently doing the same thing as
        // the verified path. Warning-only, not a hard block, so ad-hoc test
        // folders like Package4 Studio's C:\BertilTest keep working.
        if (!hasRunThroughQuickSort)
        {
            _logger.LogWarning(
                "Package4 started against {SourceLibraryPath} with no manifest.json present - this source hasn't been through QuickSort. " +
                "Falling back to a raw folder scan (no category/date metadata, no dedup, no rotation-fix). Run QuickSort first for a verified input.",
                request.SourceLibraryPath);
        }

        var manifest = hasRunThroughQuickSort
            ? await _manifestLoader.LoadAsync(request.SourceLibraryPath, cancellationToken)
            : ScanLibraryFolder(request.SourceLibraryPath);

        var state = _factory.Create(manifest, request);

        return new Package4WorkflowStartResult(Guid.NewGuid(), state, hasRunThroughQuickSort);
    }

    private static readonly HashSet<string> SkippedFolders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "@eadir", "#recycle", "#snapshot", ".@__thumb",
            "@recently-snapshot", ".synophoto", ".package1", ".package2", ".package4",
            "thumbnails", "working", "enhanced", "checkpoints", "delivery", "manifests", "temp", "smartfolders"
        };

    private static QuickSortManifest ScanLibraryFolder(string libraryPath)
    {
        var files = new List<MediaFile>();
        ScanDirectory(libraryPath, files);

        return new QuickSortManifest
        {
            WorkflowId = Guid.NewGuid(),
            RootPath = libraryPath,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            MediaFiles = files,
            FileCount = files.Count
        };
    }

    private static void ScanDirectory(string directory, List<MediaFile> result)
    {
        if (!Directory.Exists(directory))
            return;

        foreach (var file in Directory.EnumerateFiles(directory))
        {
            if (!MediaTypeHelper.IsVideo(file))
                continue;

            var fi = new FileInfo(file);
            var mediaFile = new MediaFile(file, fi.CreationTimeUtc, MediaType.Video, fi.Length);
            mediaFile.ExportedPath = file;
            result.Add(mediaFile);
        }

        foreach (var subDir in Directory.EnumerateDirectories(directory))
        {
            var name = Path.GetFileName(subDir);
            if (SkippedFolders.Contains(name) || name.StartsWith('@') || name.StartsWith('#') || name.StartsWith('.'))
            {
                continue;
            }

            ScanDirectory(subDir, result);
        }
    }
}
