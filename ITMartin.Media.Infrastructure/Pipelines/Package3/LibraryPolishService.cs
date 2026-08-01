using System.Security.Cryptography;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using ITMartin.Media.Contracts.Contracts.Runtime.Helpers;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Processing;

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
    private readonly AnthropicClient? _anthropicClient;

    public LibraryPolishService(
        ILogger<LibraryPolishService> logger,
        IDbContextFactory<MediaDbContext> dbFactory,
        IVideoMetadataService videoMetadata,
        IConfiguration configuration)
    {
        _logger = logger;
        _dbFactory = dbFactory;
        _videoMetadata = videoMetadata;

        var apiKey = configuration["Claude:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
            _anthropicClient = new AnthropicClient { ApiKey = apiKey };
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

    // Photos per Claude vision call - batched (never one call per file, see
    // CLAUDE.md) since the model can judge several photos' orientation from
    // one message just as reliably as one.
    private const int RotationBatchSize = 8;

    // Hard ceiling on how many *new* photos get checked in one run - see
    // CLAUDE.md "AI/Claude API cost discipline". A library with more unchecked
    // photos than this needs multiple clicks, on purpose.
    private const int MaxRotationChecksPerRun = 500;

    // Per-path "already resolved, never look at again" marker - keeps re-runs
    // (including after new photos are added) from re-hashing/re-checking the
    // whole library every time.
    private const string RotationCheckedFileName = "rotation-checked.json";

    // Content-hash -> degrees decisions. SmartFolders add-ons now copy real
    // files (see task #8 - symlinks -> File.Copy), so the same sideways photo
    // often exists as several byte-identical files (original + Trips copy +
    // Årbog copy + ...). Keying the decision by the *pre-rotation* hash means
    // every duplicate of an already-checked photo gets corrected for free -
    // once its bytes are actually rotated its hash changes, so it naturally
    // never re-matches this cache and never loops.
    private const string RotationDecisionsFileName = "rotation-decisions.json";

    // Folders this pass must never touch even though it otherwise walks the
    // whole tree (unlike ProtectedFolders, SmartFolders IS scanned here - its
    // copies are real, independent files that can be sideways too). Thumbnails
    // in particular are auto-generated derivatives (GalleryThumbnailService) -
    // checking them independently would double the API cost of every real
    // photo for no benefit, since they get regenerated from the source anyway.
    private static readonly HashSet<string> RotationSkipFolders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".package1", ".package2", ".package3", "_Galleri", UnplayableFolderName,
            "thumbnails", "working", "enhanced", "manifests", "temp",
        };

    // Guards against two concurrent FixOrientationAsync calls against the same
    // library racing on the same sidecar files / physically double-rotating
    // the same photo (checkpoint saves are last-write-wins, not merged).
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> RotationLocks = new(StringComparer.OrdinalIgnoreCase);

    // Rotates photos that were baked in sideways/upside-down by the pre-fix
    // HEIC/HEIF/AVIF converter (see ImageConverterService.ApplyOriginalOrientation)
    // and have no original source file left to re-derive the correct
    // orientation from - the only way left to tell is to actually look at the
    // picture. Opt-in only (real Claude API cost) - never called from
    // PolishAsync's free default pass.
    public async Task<OrientationFixResult> FixOrientationAsync(string libraryPath, CancellationToken cancellationToken = default)
    {
        if (_anthropicClient is null || !Directory.Exists(libraryPath))
            return new OrientationFixResult();

        var gate = RotationLocks.GetOrAdd(Path.GetFullPath(libraryPath), _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await FixOrientationCoreAsync(libraryPath, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<OrientationFixResult> FixOrientationCoreAsync(string libraryPath, CancellationToken cancellationToken)
    {
        var checkedPathsFile = Path.Combine(libraryPath, RotationCheckedFileName);
        var decisionsFile = Path.Combine(libraryPath, RotationDecisionsFileName);

        var checkedPaths = LoadStringSet(checkedPathsFile);
        var decisions = LoadHashDecisions(decisionsFile);

        var allImages = EnumerateImagesForRotationCheck(libraryPath, cancellationToken).ToList();
        var unresolved = allImages
            .Where(f => !checkedPaths.Contains(Path.GetRelativePath(libraryPath, f)))
            .ToList();

        var toCheck = 0;
        var rotated = 0;
        var pendingClaudeCheck = new List<string>();

        void CheckpointSave()
        {
            SaveStringSet(checkedPathsFile, checkedPaths);
            SaveHashDecisions(decisionsFile, decisions);
        }

        foreach (var file in unresolved)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string hash;
            try { hash = ComputeHash(file); }
            catch (IOException) { continue; } // file in use / vanished mid-scan - leave unchecked for next run

            if (decisions.TryGetValue(hash, out var cachedDegrees))
            {
                ApplyResolvedFile(file, cachedDegrees, ref rotated);
                checkedPaths.Add(Path.GetRelativePath(libraryPath, file));
                continue;
            }

            if (toCheck >= MaxRotationChecksPerRun) continue; // capped - remains unresolved for the next run

            pendingClaudeCheck.Add(file);
            toCheck++;

            if (pendingClaudeCheck.Count >= RotationBatchSize)
            {
                await ResolveBatchAsync(pendingClaudeCheck, libraryPath, decisions, checkedPaths, cancellationToken, r => rotated += r);
                pendingClaudeCheck.Clear();
                CheckpointSave();
            }
        }

        if (pendingClaudeCheck.Count > 0)
            await ResolveBatchAsync(pendingClaudeCheck, libraryPath, decisions, checkedPaths, cancellationToken, r => rotated += r);

        CheckpointSave();

        var remaining = allImages.Count - checkedPaths.Count;

        _logger.LogInformation(
            "Orientation fix complete for {LibraryPath}: {Checked} newly checked, {Rotated} rotated, {Remaining} remaining (capped at {Cap}/run), {Total} total images",
            libraryPath, toCheck, rotated, remaining, MaxRotationChecksPerRun, allImages.Count);

        return new OrientationFixResult
        {
            PhotosChecked  = toCheck,
            PhotosRotated  = rotated,
            RemainingCount = Math.Max(0, remaining),
        };
    }

    private async Task ResolveBatchAsync(
        List<string> files,
        string libraryPath,
        Dictionary<string, int> decisions,
        HashSet<string> checkedPaths,
        CancellationToken cancellationToken,
        Action<int> onRotated)
    {
        Dictionary<int, int> batchDecisions;
        try
        {
            batchDecisions = await CheckRotationBatchAsync(files, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Rotation check failed for a batch of {Count} photos - left unchecked for next run", files.Count);
            return;
        }

        var rotatedInBatch = 0;
        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            var degrees = batchDecisions.GetValueOrDefault(i, 0);

            string hash;
            try { hash = ComputeHash(file); }
            catch (IOException) { continue; }

            decisions[hash] = degrees;
            ApplyResolvedFile(file, degrees, ref rotatedInBatch);
            checkedPaths.Add(Path.GetRelativePath(libraryPath, file));
        }

        onRotated(rotatedInBatch);
    }

    private void ApplyResolvedFile(string file, int degrees, ref int rotatedCounter)
    {
        if (degrees == 0) return;

        try
        {
            RotateImageInPlace(file, degrees);
            rotatedCounter++;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to rotate {Path}", file);
        }
    }

    private static void RotateImageInPlace(string path, int degrees)
    {
        var mode = degrees switch
        {
            90 => RotateMode.Rotate90,
            180 => RotateMode.Rotate180,
            270 => RotateMode.Rotate270,
            _ => RotateMode.None,
        };

        if (mode == RotateMode.None) return;

        using var image = Image.Load(path);
        image.Mutate(x => x.Rotate(mode));

        // Pixels are now physically upright - a leftover EXIF Orientation tag
        // would make viewers rotate it a second time on top of this.
        image.Metadata.ExifProfile?.RemoveValue(ExifTag.Orientation);

        image.Save(path); // encoder inferred from the existing file extension
    }

    private static readonly Tool CheckRotationTool = new()
    {
        Name = "report_rotations",
        Description = "Report which photos, if any, are rotated the wrong way and need correction to appear upright",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["rotations"] = JsonDocument.Parse("""
                    {
                      "type": "array",
                      "items": {
                        "type": "object",
                        "properties": {
                          "index": { "type": "integer", "description": "0-based index of the photo, in the order shown" },
                          "degrees": { "type": "integer", "enum": [0, 90, 180, 270], "description": "Clockwise rotation needed to make the photo appear upright. 0 if it already is." }
                        },
                        "required": ["index", "degrees"]
                      },
                      "description": "Exactly one entry per photo shown"
                    }
                    """).RootElement,
            },
            Required = ["rotations"],
        },
    };

    private async Task<Dictionary<int, int>> CheckRotationBatchAsync(List<string> paths, CancellationToken cancellationToken)
    {
        var content = new List<ContentBlockParam>
        {
            new TextBlockParam
            {
                Text = "These are family photos, shown in order starting at index 0. Some may be sideways or " +
                       "upside-down (a past conversion bug lost their rotation). For each photo, report how many " +
                       "degrees clockwise it needs to be rotated to look upright: 0, 90, 180, or 270 - use 0 for " +
                       "photos that already look correct. Call report_rotations with exactly one entry per photo.",
            },
        };

        foreach (var path in paths)
        {
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            content.Add(new ImageBlockParam
            {
                Source = new Base64ImageSource { Data = Convert.ToBase64String(bytes), MediaType = GetMimeType(path) },
            });
        }

        var request = new MessageCreateParams
        {
            // Haiku, not Opus - see CLAUDE.md, this runs per-batch across a
            // whole library.
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 512,
            System = "You detect incorrectly rotated photos. Always call report_rotations with exactly one entry per photo shown, in order.",
            Tools = [CheckRotationTool],
            ToolChoice = new ToolChoiceTool { Name = "report_rotations" },
            Messages = [new() { Role = Role.User, Content = content }],
        };

        var response = await _anthropicClient!.Messages.Create(request);
        var result = new Dictionary<int, int>();

        foreach (var block in response.Content)
        {
            if (!block.TryPickToolUse(out var toolUse)) continue;

            var json = JsonSerializer.Serialize(toolUse.Input);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("rotations", out var arr)) continue;

            foreach (var item in arr.EnumerateArray())
            {
                if (item.TryGetProperty("index", out var idxEl) && idxEl.TryGetInt32(out var idx) &&
                    item.TryGetProperty("degrees", out var degEl) && degEl.TryGetInt32(out var deg) &&
                    idx >= 0 && idx < paths.Count && deg is 0 or 90 or 180 or 270)
                {
                    result[idx] = deg;
                }
            }
        }

        return result;
    }

    private IEnumerable<string> EnumerateImagesForRotationCheck(string directory, CancellationToken cancellationToken)
    {
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            if (MediaTypeHelper.IsImage(file))
                yield return file;
        }

        foreach (var subDir in Directory.EnumerateDirectories(directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (RotationSkipFolders.Contains(Path.GetFileName(subDir))) continue;

            foreach (var file in EnumerateImagesForRotationCheck(subDir, cancellationToken))
                yield return file;
        }
    }

    private static string ComputeHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string GetMimeType(string filePath) =>
        Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/jpeg"
        };

    private static HashSet<string> LoadStringSet(string path)
    {
        if (!File.Exists(path)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path));
            return list is null ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void SaveStringSet(string path, HashSet<string> values) =>
        File.WriteAllText(path, JsonSerializer.Serialize(values.ToList()));

    private static Dictionary<string, int> LoadHashDecisions(string path)
    {
        if (!File.Exists(path)) return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(path));
            return dict is null ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) : new Dictionary<string, int>(dict, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void SaveHashDecisions(string path, Dictionary<string, int> decisions) =>
        File.WriteAllText(path, JsonSerializer.Serialize(decisions));

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
