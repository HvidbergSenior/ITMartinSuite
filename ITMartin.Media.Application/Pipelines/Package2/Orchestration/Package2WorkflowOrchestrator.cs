using ITMartin.Media.Application.Pipelines.Package2.Services;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Helpers;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests.Package2;

namespace ITMartin.Media.Application.Pipelines.Package2.Orchestration;

public sealed class Package2WorkflowOrchestrator
{
    private readonly Package2WorkflowFactory
        _factory;

    private readonly Package1ManifestLoader
        _manifestLoader;

    public Package2WorkflowOrchestrator(
        Package2WorkflowFactory factory,
        Package1ManifestLoader manifestLoader)
    {
        _factory = factory;
        _manifestLoader = manifestLoader;
    }

    public async Task<Package2WorkflowStartResult>
        StartAsync(
            StartPackage2Request request,
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

        return new Package2WorkflowStartResult(
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

    private static Package1Manifest ScanLibraryFolder(string libraryPath)
    {
        var files = new List<MediaFile>();
        ScanDirectory(libraryPath, files);

        return new Package1Manifest
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