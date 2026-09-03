using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Infrastructure.Media;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;

namespace ITMartin.Media.Infrastructure.Pipelines.Package4;

// Actually opens/decodes every file rather than trusting extension or
// container metadata - catches the real "can this be played/viewed" failure
// mode, not just misclassification. Read-only: never moves, deletes, or
// rewrites anything, just reports.
public sealed class LibraryVerifyService : ILibraryVerifyService
{
    // Internal support trees, never real content - same list LibraryPolish
    // already treats as off-limits.
    private static readonly HashSet<string> ProtectedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "_Galleri", "SmartFolders", ".package1", ".package2", ".package3", ".package4", ".ReferencePhotos",
    };

    // Same Windows/OS system folders FileScanner learned to skip - never real
    // content, and $RECYCLE.BIN's per-user subfolders are access-denied.
    private static readonly HashSet<string> SystemFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "$RECYCLE.BIN", "System Volume Information",
    };

    // Danish/English pairs for every category folder QuickSort can produce -
    // see feedback_danish_english_folder_names: a library sorted under either
    // convention is still valid, this just needs to recognize both.
    private static readonly (string Category, string[] Names)[] ExpectedCategoryFolders =
    [
        ("Billeder/Images", ["Billeder", "Images"]),
        ("Videoer/Videos", ["Videoer", "Videos"]),
        ("Musik/Music", ["Musik", "Music"]),
        ("Dokumenter/Documents", ["Dokumenter", "Documents"]),
    ];

    private readonly IVideoMetadataService _videoMetadataService;
    private readonly ICollectionStore _collectionStore;
    private readonly ILogger<LibraryVerifyService> _logger;

    public LibraryVerifyService(IVideoMetadataService videoMetadataService, ICollectionStore collectionStore, ILogger<LibraryVerifyService> logger)
    {
        _videoMetadataService = videoMetadataService;
        _collectionStore = collectionStore;
        _logger = logger;
    }

    public Task<LibraryIntegrityReport> VerifyLibraryAsync(string libraryPath, CancellationToken cancellationToken = default)
    {
        var failures = new List<IntegrityFailure>();
        var total = 0;

        foreach (var file in EnumerateCheckableFiles(libraryPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            total++;

            var mediaType = MediaTypeHelper.GetMediaType(file);
            var reason = mediaType switch
            {
                MediaType.Image => CheckImage(file),
                MediaType.Video => CheckAvOrDocument(file),
                MediaType.Audio => CheckAvOrDocument(file),
                _ => CheckGeneric(file),
            };

            if (reason is not null)
            {
                _logger.LogWarning("Integrity check failed: {Path} ({Type}) - {Reason}", file, mediaType, reason);
                failures.Add(new IntegrityFailure
                {
                    RelativePath = Path.GetRelativePath(libraryPath, file),
                    MediaType = mediaType.ToString(),
                    Reason = reason,
                });
            }
        }

        _logger.LogInformation(
            "Package4 verification complete for {LibraryPath}: {Total} files checked, {Failed} failed",
            libraryPath, total, failures.Count);

        return Task.FromResult(new LibraryIntegrityReport
        {
            TotalFilesChecked = total,
            FailureCount = failures.Count,
            Failures = failures,
        });
    }

    // Metadata-only (Directory.Exists/File.Exists, no file content read) - safe
    // to run directly against a NAS-mounted path without the per-file network
    // I/O load a full VerifyLibraryAsync-style content scan would put on it.
    // That's deliberate: this exists specifically so a library doesn't have to
    // be copied back to a local disk just to check it's structured correctly.
    public async Task<LibraryStructureReport> VerifyStructureAsync(string libraryPath, CancellationToken cancellationToken = default)
    {
        var found = new List<string>();
        var missing = new List<string>();

        foreach (var (category, names) in ExpectedCategoryFolders)
        {
            if (names.Any(n => Directory.Exists(Path.Combine(libraryPath, n))))
                found.Add(category);
            else
                missing.Add(category);
        }

        var issues = new List<StructureIssue>();
        var collections = await _collectionStore.LoadAsync(libraryPath);
        var pathsChecked = 0;

        foreach (var collection in collections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var path in collection.FilePaths)
            {
                pathsChecked++;

                if (Path.IsPathRooted(path) || (path.Length >= 2 && path[1] == ':'))
                {
                    issues.Add(new StructureIssue { Context = collection.Name, Path = path, Reason = "absolute path stored - should be relative to the library root (usually recoverable via p4-repair-collections)" });
                    continue;
                }

                if (path.Contains('\\'))
                {
                    issues.Add(new StructureIssue { Context = collection.Name, Path = path, Reason = "backslash separator - breaks path resolution once served from a Linux container (the NAS)" });
                    continue;
                }

                if (!File.Exists(Path.Combine(libraryPath, path)))
                    issues.Add(new StructureIssue { Context = collection.Name, Path = path, Reason = "referenced file not found under this library root" });
            }
        }

        _logger.LogInformation(
            "Package4 structure check complete for {LibraryPath}: {Found}/{Total} expected category folders present, {Collections} collections, {Paths} paths checked, {Issues} issues",
            libraryPath, found.Count, ExpectedCategoryFolders.Length, collections.Count, pathsChecked, issues.Count);

        return new LibraryStructureReport
        {
            ExpectedFoldersFound = found,
            ExpectedFoldersMissing = missing,
            CollectionsChecked = collections.Count,
            PathsChecked = pathsChecked,
            Issues = issues,
        };
    }

    // Only ever touches collections.json - never re-sorts, never moves real
    // library content. Safe to run directly against the NAS or an external HD
    // in place, so a structure problem found by VerifyStructureAsync doesn't
    // require copying the whole library back locally to fix.
    public async Task<StructureRepairResult> RepairCollectionsPathsAsync(string libraryPath, CancellationToken cancellationToken = default)
    {
        var collectionsPath = Path.Combine(libraryPath, "collections.json");
        if (!File.Exists(collectionsPath))
            return new StructureRepairResult { CollectionsFileFound = false };

        var collections = await _collectionStore.LoadAsync(libraryPath);
        var normalized = 0;
        var recovered = 0;
        var removed = 0;

        // The leaf name of whatever root this is being run against (e.g.
        // "mie") - an absolute path baked in from a different context usually
        // still contains this exact folder name as a path segment (e.g. a
        // container mount "/library/mie/SmartFolders/..." recorded while
        // SyncGalleryCollectionsAsync ran inside the NAS's own container).
        // Everything after that segment is almost always the real relative
        // path - recovering it here beats silently dropping a whole
        // SmartFolders collection (Trips/People/Yearbook) just because it was
        // synced from inside a container once.
        var libraryLeaf = Path.GetFileName(libraryPath.TrimEnd('\\', '/'));

        foreach (var collection in collections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fixedPaths = new List<string>();
            foreach (var path in collection.FilePaths)
            {
                var candidate = path;

                if (Path.IsPathRooted(candidate) || (candidate.Length >= 2 && candidate[1] == ':'))
                {
                    var recoveredPath = TryRecoverRelativePath(candidate, libraryLeaf);
                    if (recoveredPath is not null && File.Exists(Path.Combine(libraryPath, recoveredPath)))
                    {
                        fixedPaths.Add(recoveredPath);
                        recovered++;
                    }
                    else
                    {
                        // Genuinely can't tell what this was meant to point at -
                        // safer to lose the reference than silently point at
                        // the wrong file.
                        removed++;
                    }
                    continue;
                }

                if (candidate.Contains('\\'))
                {
                    candidate = candidate.Replace('\\', '/');
                    normalized++;
                }

                if (!File.Exists(Path.Combine(libraryPath, candidate)))
                {
                    removed++;
                    continue;
                }

                fixedPaths.Add(candidate);
            }

            collection.FilePaths = fixedPaths;
        }

        await _collectionStore.SaveAsync(libraryPath, collections);

        _logger.LogInformation(
            "Package4 collections.json repair complete for {LibraryPath}: {Normalized} paths normalized, {Recovered} absolute paths recovered, {Removed} unresolvable paths dropped",
            libraryPath, normalized, recovered, removed);

        return new StructureRepairResult
        {
            CollectionsFileFound = true,
            NormalizedPaths = normalized,
            RecoveredAbsolutePaths = recovered,
            RemovedMissingPaths = removed,
        };
    }

    // Matches the current date-range group label shapes LibraryExportService
    // produces (see [[project_package1_month_split]]): "dd-dd MonthName"
    // when a group stays in one calendar month, "Mon-Mon" (3-letter Danish
    // abbreviations) when it spans two. A Year folder's own subfolders
    // should always match one of these once that year is busy enough to
    // need subfolders at all - anything else (a leftover calendar "MM-Month"
    // folder from the superseded design, a stray "Juni"-style mistake, an
    // unrelated folder) is a real structure issue.
    private static readonly System.Text.RegularExpressions.Regex GroupLabelPattern =
        new(@"^(\d{2}-\d{2} \p{L}+|[A-ZÆØÅ][a-zæøå]{2}-[A-ZÆØÅ][a-zæøå]{2})$");

    private const int GroupFlatThreshold = 50; // must match LibraryExportService.GroupTargetSize

    public Task<DeliveryStructureReport> VerifyDeliveryStructureAsync(string libraryPath, CancellationToken cancellationToken = default)
    {
        var extensionsByCategory = new Dictionary<string, List<string>>();
        var issues = new List<DeliveryStructureIssue>();
        var yearFoldersChecked = 0;

        foreach (var (categoryLabel, names) in ExpectedCategoryFolders)
        {
            var categoryDir = names.Select(n => Path.Combine(libraryPath, n)).FirstOrDefault(Directory.Exists);
            if (categoryDir is null) continue;

            var extensions = Directory.EnumerateFiles(categoryDir, "*", SearchOption.AllDirectories)
                .Where(f => !Path.GetFileName(Path.GetDirectoryName(f) ?? "").Equals("thumbnails", StringComparison.OrdinalIgnoreCase))
                .Select(f => Path.GetExtension(f).ToLowerInvariant())
                .Where(e => e.Length > 0)
                .Distinct()
                .OrderBy(e => e)
                .ToList();
            extensionsByCategory[categoryLabel] = extensions;

            // Musik is organized by Artist/Album, not Year - the group-label
            // structure check below doesn't apply to it at all.
            if (categoryLabel.StartsWith("Musik")) continue;

            foreach (var yearDir in Directory.EnumerateDirectories(categoryDir).Where(d => System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(d), @"^\d{4}$")))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yearFoldersChecked++;

                var totalFiles = Directory.EnumerateFiles(yearDir, "*", SearchOption.AllDirectories)
                    .Count(f => !Path.GetFileName(Path.GetDirectoryName(f) ?? "").Equals("thumbnails", StringComparison.OrdinalIgnoreCase));
                var subDirs = Directory.EnumerateDirectories(yearDir)
                    .Where(d => !Path.GetFileName(d).Equals("thumbnails", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (totalFiles <= GroupFlatThreshold && subDirs.Count > 0)
                {
                    issues.Add(new DeliveryStructureIssue
                    {
                        RelativePath = Path.GetRelativePath(libraryPath, yearDir),
                        Reason = $"only {totalFiles} files (≤{GroupFlatThreshold}) but has {subDirs.Count} subfolder(s) - should be flat",
                    });
                }
                else if (totalFiles > GroupFlatThreshold)
                {
                    foreach (var subDir in subDirs.Where(d => !GroupLabelPattern.IsMatch(Path.GetFileName(d))))
                    {
                        issues.Add(new DeliveryStructureIssue
                        {
                            RelativePath = Path.GetRelativePath(libraryPath, subDir),
                            Reason = "subfolder name doesn't match the current date-range group label pattern - likely leftover from a superseded structure",
                        });
                    }
                }
            }
        }

        _logger.LogInformation(
            "Delivery structure check complete for {LibraryPath}: {Years} year folders checked, {Issues} issues",
            libraryPath, yearFoldersChecked, issues.Count);

        return Task.FromResult(new DeliveryStructureReport
        {
            YearFoldersChecked = yearFoldersChecked,
            ExtensionsByCategory = extensionsByCategory,
            Issues = issues,
        });
    }

    // Finds libraryLeafName as a whole path segment inside absolutePath and
    // returns everything after it - null if the segment never appears at all.
    private static string? TryRecoverRelativePath(string absolutePath, string libraryLeafName)
    {
        if (string.IsNullOrEmpty(libraryLeafName)) return null;

        var normalized = absolutePath.Replace('\\', '/');
        var marker = "/" + libraryLeafName + "/";
        var idx = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return idx < 0 ? null : normalized[(idx + marker.Length)..];
    }

    // Real decode attempt, not just "does ImageSharp recognize the
    // extension" - a truncated/corrupt file throws here even if the header
    // looks fine.
    private static string? CheckImage(string path)
    {
        try
        {
            using var image = Image.Load(path);
            if (image.Width == 0 || image.Height == 0)
                return "decoded but reports zero dimensions";
            return null;
        }
        catch (Exception ex)
        {
            return $"could not decode image: {ex.Message}";
        }
    }

    // ffprobe returning null codec is the same signal MediaRulesWorkflowStep
    // already trusts for "not actually playable" - reused here instead of a
    // second probing implementation. Works for both video and audio since
    // ffprobe isn't format-restricted despite the interface's name.
    private string? CheckAvOrDocument(string path)
    {
        try
        {
            var codec = _videoMetadataService.GetVideoCodec(path);
            if (codec is null)
                return "ffprobe could not read a media stream (corrupt/unreadable)";

            var duration = _videoMetadataService.GetDuration(path);
            if (duration is null or { TotalSeconds: <= 0 })
                return $"codec '{codec}' read, but duration is zero/unreadable";

            return null;
        }
        catch (Exception ex)
        {
            return $"probe failed: {ex.Message}";
        }
    }

    // No format-specific validator for documents (PDFs, etc.) - just confirm
    // the file is actually readable and non-empty. Better than nothing;
    // upgrade later if a real PDF/doc corruption case shows up in practice.
    private static string? CheckGeneric(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists) return "file vanished during scan";
            if (info.Length == 0) return "zero-byte file";

            using var stream = File.OpenRead(path);
            var buffer = new byte[Math.Min(4096, info.Length)];
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read == 0) return "could not read any bytes";

            return null;
        }
        catch (Exception ex)
        {
            return $"could not open file: {ex.Message}";
        }
    }

    private static IEnumerable<string> EnumerateCheckableFiles(string root)
    {
        if (!Directory.Exists(root)) yield break;

        foreach (var file in Directory.EnumerateFiles(root))
            yield return file;

        foreach (var subDir in Directory.EnumerateDirectories(root))
        {
            var name = Path.GetFileName(subDir);
            if (ProtectedFolders.Contains(name) || SystemFolders.Contains(name) ||
                name.StartsWith('@') || name.StartsWith('#') || name.StartsWith('.'))
                continue;

            foreach (var file in EnumerateCheckableFiles(subDir))
                yield return file;
        }
    }
}
