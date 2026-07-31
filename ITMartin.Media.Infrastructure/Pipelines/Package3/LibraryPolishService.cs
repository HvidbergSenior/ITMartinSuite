using ITMartin.Media.Contracts.Contracts.Runtime.Helpers;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Infrastructure.Pipelines.Package3;

public sealed class LibraryPolishService : ILibraryPolishService
{
    // Root-level folder that unplayable videos get quarantined into - not
    // real content, never shown in the gallery (see gallery-web's
    // RootFoldersHiddenFromBrowsing).
    public const string UnplayableFolderName = "Afspilningsfejl";

    // OS-generated cache files that sometimes leak in from the original
    // source folder (e.g. a Windows Explorer thumbnail cache) - never real
    // photo content, safe to remove outright.
    private static readonly HashSet<string> JunkFileNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "thumbs.db", "desktop.ini", ".ds_store", ".spotlight-v100", ".trashes",
        };

    private static readonly Func<string, bool> IsJunkFile = name =>
        JunkFileNames.Contains(name) ||
        (name.StartsWith("thumbs_", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".db", StringComparison.OrdinalIgnoreCase));

    // Internal support trees this pass must never touch - own lifecycle,
    // may legitimately contain files this service would otherwise flag.
    private static readonly HashSet<string> ProtectedFolders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "_Galleri", "SmartFolders", ".package1", ".package2", ".package3", ".ReferencePhotos",
            UnplayableFolderName,
        };

    private readonly ILogger<LibraryPolishService> _logger;
    private readonly IDbContextFactory<MediaDbContext> _dbFactory;
    private readonly IVideoMetadataService _videoMetadata;

    public LibraryPolishService(
        ILogger<LibraryPolishService> logger,
        IDbContextFactory<MediaDbContext> dbFactory,
        IVideoMetadataService videoMetadata)
    {
        _logger = logger;
        _dbFactory = dbFactory;
        _videoMetadata = videoMetadata;
    }

    public async Task<LibraryPolishResult> PolishAsync(string libraryPath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(libraryPath))
            return new LibraryPolishResult();

        var junkRemoved = RemoveJunkFiles(libraryPath, cancellationToken);
        var manifestsHidden = HideManifests(libraryPath);
        var screenshotsFixed = await FixMisclassifiedScreenshotsAsync(libraryPath, cancellationToken);
        var unplayableQuarantined = QuarantineUnplayableVideos(libraryPath, cancellationToken);
        var emptyFoldersRemoved = RemoveEmptyFolders(libraryPath, cancellationToken, isRoot: true);

        _logger.LogInformation(
            "Library polish complete for {LibraryPath}: {Junk} junk files removed, {Manifests} manifests hidden, {Screenshots} misclassified screenshots fixed, {Unplayable} unplayable videos quarantined, {Empty} empty folders removed",
            libraryPath, junkRemoved, manifestsHidden, screenshotsFixed, unplayableQuarantined, emptyFoldersRemoved);

        return new LibraryPolishResult
        {
            EmptyFoldersRemoved = emptyFoldersRemoved,
            JunkFilesRemoved = junkRemoved,
            ManifestsHidden = manifestsHidden,
            MisclassifiedScreenshotsFixed = screenshotsFixed,
            UnplayableVideosQuarantined = unplayableQuarantined,
        };
    }

    // Two checks, cheapest first: ffprobe reading the container header catches
    // a missing/zero duration outright (truncated exports, a real problem
    // after a large phone-cloud sync); a short ffmpeg decode of the first few
    // seconds then catches files with valid-looking metadata but an actually
    // broken/unsupported video stream - duration alone missed real playback
    // failures a customer would hit in the browser. Capped to a few seconds so
    // a multi-GB film doesn't need a full decode just to prove it plays.
    // Quarantined rather than deleted since "can't decode the start" isn't
    // proof the file is unrecoverable - just that it shouldn't be shown to the
    // customer as-is. Flat folder (no year/month structure) since these need a
    // human to look at them, not browse them.
    private int QuarantineUnplayableVideos(string libraryPath, CancellationToken cancellationToken)
    {
        var quarantineFolder = Path.Combine(libraryPath, UnplayableFolderName);
        var quarantined = 0;

        foreach (var file in EnumerateVideos(libraryPath, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            TimeSpan? duration;
            try { duration = _videoMetadata.GetDuration(file); }
            catch { duration = null; }

            var hasDuration = duration is not null && duration.Value > TimeSpan.Zero;
            if (hasDuration && CanDecodeStart(file)) continue;

            Directory.CreateDirectory(quarantineFolder);
            var destName = Path.GetFileName(file);
            var destPath = Path.Combine(quarantineFolder, destName);
            var attempt = 1;
            while (File.Exists(destPath))
            {
                destPath = Path.Combine(quarantineFolder,
                    $"{Path.GetFileNameWithoutExtension(destName)}_{attempt}{Path.GetExtension(destName)}");
                attempt++;
            }

            try
            {
                File.Move(file, destPath);
                quarantined++;
            }
            catch (IOException) { /* best effort - skip files in use */ }
        }

        return quarantined;
    }

    // Actually decodes the first couple of seconds (not just reading the
    // header) - catches a truncated/corrupt video stream that ffprobe alone
    // reports a perfectly valid duration for. `-xerror` makes ffmpeg stop and
    // fail on the first decode error instead of skipping past corrupt frames.
    private static bool CanDecodeStart(string file)
    {
        try
        {
            var ffmpegPath = OperatingSystem.IsWindows()
                ? Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe")
                : "ffmpeg";

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffmpegPath,
                ArgumentList = { "-v", "error", "-xerror", "-t", "3", "-i", file, "-f", "null", "-" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process is null) return false;

            var stderr = process.StandardError.ReadToEnd();
            process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return process.ExitCode == 0 && string.IsNullOrWhiteSpace(stderr);
        }
        catch
        {
            return false;
        }
    }

    private IEnumerable<string> EnumerateVideos(string directory, CancellationToken cancellationToken)
    {
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            if (MediaTypeHelper.IsVideo(file))
                yield return file;
        }

        foreach (var subDir in Directory.EnumerateDirectories(directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ProtectedFolders.Contains(Path.GetFileName(subDir))) continue;

            foreach (var file in EnumerateVideos(subDir, cancellationToken))
                yield return file;
        }
    }

    // Real screenshot tools save lossless PNG; a JPG sitting in a "Screenshots"
    // folder is almost always a real photo that coincidentally matched a phone
    // screen resolution. Moves it to the
    // parallel "Images" path (same Year/Month structure) and repoints any
    // MediaFaces rows so face-matching keeps working after the move.
    private async Task<int> FixMisclassifiedScreenshotsAsync(string libraryPath, CancellationToken cancellationToken)
    {
        var screenshotsFolders = new List<string>();
        FindFoldersNamed(libraryPath, "Screenshots", screenshotsFolders, cancellationToken);
        if (screenshotsFolders.Count == 0) return 0;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var fixedCount = 0;

        foreach (var screenshotsDir in screenshotsFolders)
        {
            foreach (var file in Directory.EnumerateFiles(screenshotsDir, "*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file);
                if (!ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) &&
                    !ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
                    continue;

                var relativeToScreenshots = Path.GetRelativePath(screenshotsDir, file);
                var parentOfScreenshots = Path.GetDirectoryName(screenshotsDir)!;
                var newPath = Path.Combine(parentOfScreenshots, "Images", relativeToScreenshots);

                // Same-named file already at the destination (camera filename
                // reuse is common) - disambiguate rather than silently leave
                // this one miscategorized.
                var attempt = 1;
                while (File.Exists(newPath))
                {
                    var dir = Path.GetDirectoryName(relativeToScreenshots) ?? "";
                    var baseName = Path.GetFileNameWithoutExtension(relativeToScreenshots);
                    var candidateName = $"{baseName}_{attempt}{ext}";
                    newPath = Path.Combine(parentOfScreenshots, "Images", dir, candidateName);
                    attempt++;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
                File.Move(file, newPath);

                var faces = await db.MediaFaces.Where(f => f.MediaFilePath == file).ToListAsync(cancellationToken);
                foreach (var face in faces) face.MediaFilePath = newPath;

                fixedCount++;
            }
        }

        if (fixedCount > 0) await db.SaveChangesAsync(cancellationToken);
        return fixedCount;
    }

    private void FindFoldersNamed(string directory, string name, List<string> results, CancellationToken cancellationToken)
    {
        foreach (var subDir in Directory.EnumerateDirectories(directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dirName = Path.GetFileName(subDir);
            if (ProtectedFolders.Contains(dirName)) continue;

            if (dirName.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(subDir);
                continue;
            }

            FindFoldersNamed(subDir, name, results, cancellationToken);
        }
    }

    private int RemoveJunkFiles(string directory, CancellationToken cancellationToken)
    {
        var removed = 0;

        foreach (var file in Directory.EnumerateFiles(directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsJunkFile(Path.GetFileName(file))) continue;

            try
            {
                File.Delete(file);
                removed++;
            }
            catch (IOException) { /* best effort - skip files in use */ }
        }

        foreach (var subDir in Directory.EnumerateDirectories(directory))
        {
            if (ProtectedFolders.Contains(Path.GetFileName(subDir))) continue;
            removed += RemoveJunkFiles(subDir, cancellationToken);
        }

        return removed;
    }

    private static int HideManifests(string libraryPath)
    {
        var manifestPath = Path.Combine(libraryPath, "manifest.json");
        if (!File.Exists(manifestPath)) return 0;

        var attributes = File.GetAttributes(manifestPath);
        if (attributes.HasFlag(FileAttributes.Hidden)) return 0;

        File.SetAttributes(manifestPath, attributes | FileAttributes.Hidden);
        return 1;
    }

    // Bottom-up so a folder emptied by RemoveJunkFiles (or by this same pass
    // clearing out an emptied child folder) still gets cleaned up in one run.
    private int RemoveEmptyFolders(string directory, CancellationToken cancellationToken, bool isRoot = false)
    {
        var removed = 0;

        foreach (var subDir in Directory.EnumerateDirectories(directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ProtectedFolders.Contains(Path.GetFileName(subDir))) continue;

            removed += RemoveEmptyFolders(subDir, cancellationToken);
        }

        if (!isRoot &&
            !Directory.EnumerateFileSystemEntries(directory).Any())
        {
            try
            {
                Directory.Delete(directory);
                removed++;
            }
            catch (IOException) { /* best effort */ }
        }

        return removed;
    }
}
