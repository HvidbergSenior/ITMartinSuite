using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
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
using SixLabors.ImageSharp.Formats.Jpeg;
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

    // Same threshold and reasoning as DuplicateService's Package1 pass -
    // below this many differing bits (of 64) two images are treated as the
    // same photo re-saved at a different quality rather than two distinct
    // photos.
    private const int NearDuplicateHammingThreshold = 6;

    private readonly ILogger<LibraryPolishService> _logger;
    private readonly IDbContextFactory<MediaDbContext> _dbFactory;
    private readonly IVideoMetadataService _videoMetadata;
    private readonly IMediaDateService _mediaDateService;
    private readonly IExifService _exifService;
    private readonly IPerceptualHashService _perceptualHashService;
    private readonly AnthropicClient? _anthropicClient;

    public LibraryPolishService(
        ILogger<LibraryPolishService> logger,
        IDbContextFactory<MediaDbContext> dbFactory,
        IVideoMetadataService videoMetadata,
        IMediaDateService mediaDateService,
        IExifService exifService,
        IPerceptualHashService perceptualHashService,
        IConfiguration configuration)
    {
        _logger = logger;
        _dbFactory = dbFactory;
        _videoMetadata = videoMetadata;
        _mediaDateService = mediaDateService;
        _exifService = exifService;
        _perceptualHashService = perceptualHashService;

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

    // English subfolder names inside Udaterede (this is where Package1 drops
    // undated files) map to the Danish top-level category names the rest of
    // the library uses. Screenshots deliberately excluded - that top-level
    // folder is flat, not year/month organized, so there's nowhere dated to
    // move one to.
    private static readonly (string SourceSubFolder, string Category)[] RedatableCategories =
    [
        ("Images", "Billeder"),
        ("Videos", "Videoer"),
    ];

    public Task<RedateUndatedResult> RedateUndatedAsync(string libraryPath, CancellationToken cancellationToken = default)
    {
        var checkedCount = 0;
        var moved = 0;

        foreach (var (sourceSubFolder, category) in RedatableCategories)
        {
            var sourceDir = Path.Combine(libraryPath, "Udaterede", sourceSubFolder);
            if (!Directory.Exists(sourceDir)) continue;

            foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories).ToList())
            {
                cancellationToken.ThrowIfCancellationRequested();
                checkedCount++;

                MediaDateResult dateResult;
                try
                {
                    dateResult = _mediaDateService.GetBestDate(new MediaDateRequest(file));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to re-check date for {Path}", file);
                    continue;
                }

                if (!dateResult.IsReliable || dateResult.Date is not { } date) continue;

                var monthFolder = $"{date.Month:00}-{new DateTime(date.Year, date.Month, 1):MMMM}";
                var targetDir = Path.Combine(libraryPath, category, date.Year.ToString(), monthFolder);

                try
                {
                    Directory.CreateDirectory(targetDir);
                    var targetPath = ResolveNameCollision(Path.Combine(targetDir, Path.GetFileName(file)));
                    File.Move(file, targetPath);
                    moved++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to move re-dated file {Path} to {Category}/{Year}/{Month}", file, category, date.Year, monthFolder);
                }
            }
        }

        _logger.LogInformation(
            "Re-date pass complete for {LibraryPath}: {Checked} checked, {Moved} moved out of Udaterede",
            libraryPath, checkedCount, moved);

        return Task.FromResult(new RedateUndatedResult
        {
            Checked = checkedCount,
            Moved = moved,
            StillUndated = checkedCount - moved,
        });
    }

    public Task<CameraGroupResult> GroupByCameraMakeAsync(
        string libraryPath, string makeContains, string targetFolderName, CancellationToken cancellationToken = default)
    {
        var checkedCount = 0;
        var moved = 0;

        var billederDir = Path.Combine(libraryPath, "Billeder");
        if (!Directory.Exists(billederDir))
            return Task.FromResult(new CameraGroupResult());

        var targetDir = Path.Combine(libraryPath, targetFolderName);

        foreach (var file in Directory.EnumerateFiles(billederDir, "*", SearchOption.AllDirectories).ToList())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ITMartin.Media.Infrastructure.Media.MediaTypeHelper.IsImage(file)) continue;
            checkedCount++;

            (string? Make, string? Model, string? Software)? meta;
            try
            {
                meta = _exifService.ReadMetadata(file);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read EXIF for {Path}", file);
                continue;
            }

            var matches = (meta?.Make?.Contains(makeContains, StringComparison.OrdinalIgnoreCase) ?? false) ||
                          (meta?.Model?.Contains(makeContains, StringComparison.OrdinalIgnoreCase) ?? false);
            if (!matches) continue;

            try
            {
                Directory.CreateDirectory(targetDir);
                var targetPath = ResolveNameCollision(Path.Combine(targetDir, Path.GetFileName(file)));
                File.Move(file, targetPath);
                moved++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to move {Path} into {Folder}", file, targetFolderName);
            }
        }

        _logger.LogInformation(
            "Camera-group pass complete for {LibraryPath}: {Checked} checked, {Moved} moved into {Folder}",
            libraryPath, checkedCount, moved, targetFolderName);

        return Task.FromResult(new CameraGroupResult { Checked = checkedCount, Moved = moved });
    }

    public async Task<DeduplicateResult> DeduplicateFolderAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(folderPath))
            return new DeduplicateResult();

        var byHash = new Dictionary<string, List<string>>();
        var allFiles = new List<string>();
        var checkedCount = 0;

        foreach (var file in Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            checkedCount++;
            allFiles.Add(file);

            string hash;
            try
            {
                hash = ComputeHash(file);
            }
            catch (IOException)
            {
                continue; // file in use / vanished mid-scan - leave it, not worth failing the whole pass
            }

            if (!byHash.TryGetValue(hash, out var group))
            {
                group = [];
                byHash[hash] = group;
            }
            group.Add(file);
        }

        var deleted = 0;
        var handled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in byHash.Values.Where(g => g.Count > 1))
        {
            // Deterministic, not meaningful - both filenames point at the
            // exact same bytes, so which name survives doesn't matter beyond
            // being reproducible if this pass ever runs again.
            var ordered = group.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var f in ordered) handled.Add(f);

            foreach (var duplicate in ordered.Skip(1))
            {
                try
                {
                    File.Delete(duplicate);
                    deleted++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete duplicate {Path}", duplicate);
                }
            }
        }

        // Near-duplicates: catches the same "same photo, different bytes"
        // case as DuplicateService's Package1 pass (recompressed re-imports
        // etc.), for already-sorted libraries where a scoped polish pass -
        // not a full Package1 re-run - is the right fix (see
        // feedback_package1_not_idempotent). Bucketed by containing folder,
        // since files landing here are already organized into Year/Month
        // folders and that's exactly where these collisions happen.
        var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".heic", ".webp"
        };

        var nearDuplicateGroups = 0;

        foreach (var folderGroup in allFiles
                     .Where(f => !handled.Contains(f) && imageExtensions.Contains(Path.GetExtension(f)))
                     .GroupBy(f => Path.GetDirectoryName(f) ?? folderPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var hashed = new List<(string Path, ulong Hash, long Size)>();

            foreach (var file in folderGroup)
            {
                var hash = await _perceptualHashService.ComputeAsync(file, cancellationToken);
                if (hash is { } h)
                {
                    long size;
                    try { size = new FileInfo(file).Length; }
                    catch (IOException) { continue; }

                    hashed.Add((file, h, size));
                }
            }

            var used = new bool[hashed.Count];

            for (var i = 0; i < hashed.Count; i++)
            {
                if (used[i]) continue;

                var members = new List<(string Path, ulong Hash, long Size)> { hashed[i] };

                for (var j = i + 1; j < hashed.Count; j++)
                {
                    if (used[j]) continue;

                    if (_perceptualHashService.HammingDistance(hashed[i].Hash, hashed[j].Hash) <= NearDuplicateHammingThreshold)
                    {
                        members.Add(hashed[j]);
                        used[j] = true;
                    }
                }

                if (members.Count <= 1) continue;

                used[i] = true;
                nearDuplicateGroups++;

                // Largest file wins - the smaller copies are the ones that
                // got recompressed harder on their way back into the library.
                var keep = members.OrderByDescending(m => m.Size).First();
                foreach (var loser in members.Where(m => m.Path != keep.Path))
                {
                    try
                    {
                        File.Delete(loser.Path);
                        deleted++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete near-duplicate {Path}", loser.Path);
                    }
                }
            }
        }

        _logger.LogInformation(
            "Deduplicate pass complete for {FolderPath}: {Checked} checked, {Deleted} duplicates removed ({NearDuplicateGroups} were near-duplicate/recompressed matches)",
            folderPath, checkedCount, deleted, nearDuplicateGroups);

        return new DeduplicateResult { Checked = checkedCount, Deleted = deleted };
    }

    private static string ResolveNameCollision(string path)
    {
        if (!File.Exists(path)) return path;

        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        var i = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            i++;
        } while (File.Exists(candidate));
        return candidate;
    }

    // Photos per Claude vision call - batched (never one call per file, see
    // CLAUDE.md) since the model can judge several photos' orientation from
    // one message just as reliably as one.
    private const int RotationBatchSize = 8;

    // Hard ceiling on how many *new* photos get checked in one run - see
    // CLAUDE.md "AI/Claude API cost discipline". A library with more unchecked
    // photos than this needs multiple clicks, on purpose.
    private const int MaxRotationChecksPerRun = 500;

    // Batches are independent Claude calls - running several at once doesn't
    // change how many calls happen (same batching as before), just how long
    // the whole run takes. Same pattern/value as ImageTaggingService.
    private const int RotationConcurrency = 8;

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
        var batches = new List<List<string>>();
        var pendingClaudeCheck = new List<string>();

        var stateLock = new object();

        void CheckpointSave()
        {
            lock (stateLock)
            {
                SaveStringSet(checkedPathsFile, checkedPaths);
                SaveHashDecisions(decisionsFile, decisions);
            }
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
                batches.Add(pendingClaudeCheck);
                pendingClaudeCheck = new List<string>();
            }
        }

        if (pendingClaudeCheck.Count > 0)
            batches.Add(pendingClaudeCheck);

        // Same total number of batches/Claude calls as running them one at a
        // time would produce - this only shortens wall-clock time by letting
        // several independent batch calls be in flight at once (see
        // CLAUDE.md: fix the ratio via batching first, concurrency second).
        var completedBatches = 0;
        await Parallel.ForEachAsync(
            batches,
            new ParallelOptions { MaxDegreeOfParallelism = RotationConcurrency, CancellationToken = cancellationToken },
            async (batch, ct) =>
            {
                await ResolveBatchAsync(batch, libraryPath, decisions, checkedPaths, stateLock, ct, r => Interlocked.Add(ref rotated, r));

                if (Interlocked.Increment(ref completedBatches) % RotationConcurrency == 0)
                    CheckpointSave();
            });

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
        object stateLock,
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

        // Hashing runs unlocked (per-file I/O, safe to overlap across
        // concurrent batches) - only the shared dictionaries and the actual
        // in-place rotate are serialized, since those aren't thread-safe.
        var rotatedInBatch = 0;
        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            var degrees = batchDecisions.GetValueOrDefault(i, 0);

            string hash;
            try { hash = ComputeHash(file); }
            catch (IOException) { continue; }

            lock (stateLock)
            {
                decisions[hash] = degrees;
                ApplyResolvedFile(file, degrees, ref rotatedInBatch);
                checkedPaths.Add(Path.GetRelativePath(libraryPath, file));
            }
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
            var resized = await ResizeForRotationCheckAsync(path, cancellationToken);
            content.Add(new ImageBlockParam
            {
                Source = new Base64ImageSource { Data = Convert.ToBase64String(resized), MediaType = "image/jpeg" },
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

    // Claude vision doesn't need full resolution to judge sideways/upside-down
    // - camera originals here run 2-3MB+ each, and 8 of them in one batch (see
    // RotationBatchSize) was hitting Anthropic's request-size limit outright
    // (RequestEntityTooLarge), silently leaving every affected batch
    // unresolved forever (retried every run, never succeeding). Downscaling
    // well under Anthropic's own resize threshold fixes that and cuts image
    // tokens - cost, not just reliability - with no loss of judgment accuracy
    // for a task this coarse (0/90/180/270).
    private static async Task<byte[]> ResizeForRotationCheckAsync(string path, CancellationToken cancellationToken)
    {
        const int MaxDimension = 768;
        using var image = await Image.LoadAsync(path, cancellationToken);
        if (image.Width > MaxDimension || image.Height > MaxDimension)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(MaxDimension, MaxDimension),
            }));
        }
        using var ms = new MemoryStream();
        await image.SaveAsJpegAsync(ms, new JpegEncoder { Quality = 80 }, cancellationToken);
        return ms.ToArray();
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
                var newRelativePath = Path.GetRelativePath(libraryPath, newPath).Replace('\\', '/');
                foreach (var face in faces)
                {
                    face.MediaFilePath = newPath;
                    face.RelativePath = newRelativePath;
                }

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
