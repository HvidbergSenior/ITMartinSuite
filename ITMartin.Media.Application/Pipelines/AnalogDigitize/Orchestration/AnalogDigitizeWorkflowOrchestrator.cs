using ITMartin.Media.Application.Pipelines.AnalogDigitize.Services;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Helpers;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests.AnalogDigitize;

namespace ITMartin.Media.Application.Pipelines.AnalogDigitize.Orchestration;

public sealed class AnalogDigitizeWorkflowOrchestrator
{
    private readonly AnalogDigitizeWorkflowFactory
        _factory;

    private readonly QuickSortManifestLoader
        _manifestLoader;

    public AnalogDigitizeWorkflowOrchestrator(
        AnalogDigitizeWorkflowFactory factory,
        QuickSortManifestLoader manifestLoader)
    {
        _factory = factory;
        _manifestLoader = manifestLoader;
    }

    public async Task<AnalogDigitizeWorkflowStartResult>
        StartAsync(
            StartAnalogDigitizeRequest request,
            CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(
            request.SourceLibraryPath,
            "manifest.json");

        var manifest = File.Exists(manifestPath)
            ? await _manifestLoader.LoadAsync(
                request.SourceLibraryPath,
                cancellationToken)
            : ScanLibraryFolder(request.SourceLibraryPath);

        var state =
            _factory.Create(
                manifest,
                request);

        return new AnalogDigitizeWorkflowStartResult(
            Guid.NewGuid(),
            state);
    }

    private static readonly HashSet<string> SkippedFolders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "@eadir", "#recycle", "#snapshot", ".@__thumb",
            "@recently-snapshot", ".synophoto", ".package1", ".package2",
            "thumbnails", "working", "enhanced", "manifests", "temp", "smartfolders"
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
            if (!MediaTypeHelper.IsVideo(file) && !MediaTypeHelper.IsImage(file))
                continue;

            var fi = new FileInfo(file);
            var mediaType = MediaTypeHelper.IsVideo(file) ? MediaType.Video : MediaType.Image;
            var mediaFile = new MediaFile(file, fi.CreationTimeUtc, mediaType, fi.Length);
            mediaFile.ExportedPath = file;
            result.Add(mediaFile);
        }

        foreach (var subDir in Directory.EnumerateDirectories(directory))
        {
            var name = Path.GetFileName(subDir);
            if (SkippedFolders.Contains(name) ||
                name.StartsWith('@') ||
                name.StartsWith('#') ||
                name.StartsWith('.'))
            {
                continue;
            }

            ScanDirectory(subDir, result);
        }
    }
}