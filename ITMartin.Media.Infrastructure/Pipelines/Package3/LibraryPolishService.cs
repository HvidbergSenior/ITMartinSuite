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

    // Same quarantine pattern as UnplayableFolderName, applied to photos the
    // free rotation-check genuinely can't resolve (no face detected at any
    // rotation - the fast tier's answer will never change on a re-run, since
    // the ONNX model doesn't change between runs). Moving them out of the
    // main Year/Month structure - rather than leaving them in place with
    // RotationIsCorrect=false forever - is what keeps every ordinary run
    // fast and bounded: the main scan skips this folder entirely (see
    // RotationSkipFolders/ClassifySkipFolders), so the same doomed files
    // never get re-decoded and re-checked every time. This is the one place
    // meant to be pointed at the paid FixOrientationAsync tier later, on
    // demand, instead of a whole-library rescan.
    public const string RotationUnknownFolderName = "RotationUkendt";

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
            UnplayableFolderName, RotationUnknownFolderName,
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
    // Called once, sequentially - the orientation pre-check runs one file at
    // a time inside the main foreach (unlike Package3Service's parallel face
    // indexing, which needs a pool of instances for concurrent ONNX calls).
    private readonly IFaceRecognitionService _faceRecognitionService;
    // Kept alongside the single instance above so DetectRotatedImagesAsync can
    // create its own independent instance per parallel partition instead -
    // FaceOnnxRecognitionService serializes every call behind an internal
    // lock, so sharing one instance across concurrent workers wouldn't
    // actually parallelize anything (see AddAi's own comment on this factory).
    private readonly Func<IFaceRecognitionService> _faceRecognitionFactory;
    private readonly IImageAnalysisService _imageAnalysis;
    private readonly IDuplicateService _duplicateService;
    private readonly IFileStatusRegistryService _fileStatusRegistry;
    private readonly AnthropicClient? _anthropicClient;

    public LibraryPolishService(
        ILogger<LibraryPolishService> logger,
        IDbContextFactory<MediaDbContext> dbFactory,
        IVideoMetadataService videoMetadata,
        IMediaDateService mediaDateService,
        IExifService exifService,
        IPerceptualHashService perceptualHashService,
        Func<IFaceRecognitionService> faceRecognitionFactory,
        IImageAnalysisService imageAnalysis,
        IDuplicateService duplicateService,
        IFileStatusRegistryService fileStatusRegistry,
        IConfiguration configuration)
    {
        _logger = logger;
        _dbFactory = dbFactory;
        _videoMetadata = videoMetadata;
        _mediaDateService = mediaDateService;
        _exifService = exifService;
        _perceptualHashService = perceptualHashService;
        _faceRecognitionFactory = faceRecognitionFactory;
        _faceRecognitionService = faceRecognitionFactory();
        _imageAnalysis = imageAnalysis;
        _duplicateService = duplicateService;
        _fileStatusRegistry = fileStatusRegistry;

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

    // Subfolder names inside the undated top-level folder map 1:1 to
    // CategoryHelper's own category names, whichever naming convention this
    // particular library was sorted with - Package1's default changed from
    // English to Danish 2026-08-20 (see CategoryHelper), but existing
    // already-sorted libraries are never renamed, so both must keep working.
    // Screenshots deliberately excluded - that top-level folder is flat, not
    // year/month organized, so there's nowhere dated to move one to.
    private static readonly (string SourceSubFolder, string Category)[] RedatableCategories =
    [
        ("Images", "Images"),
        ("Videos", "Videos"),
        ("Billeder", "Billeder"),
        ("Videoer", "Videoer"),
    ];

    private static readonly string[] UndatedFolderNames = ["Undated", "Udaterede"];

    public Task<RedateUndatedResult> RedateUndatedAsync(string libraryPath, CancellationToken cancellationToken = default)
    {
        var checkedCount = 0;
        var moved = 0;

        foreach (var (sourceSubFolder, category) in RedatableCategories)
        {
            var undatedFolder = UndatedFolderNames.FirstOrDefault(f => Directory.Exists(Path.Combine(libraryPath, f, sourceSubFolder)));
            if (undatedFolder is null) continue;
            var sourceDir = Path.Combine(libraryPath, undatedFolder, sourceSubFolder);
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
            "Re-date pass complete for {LibraryPath}: {Checked} checked, {Moved} moved out of Undated",
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
            ".package1", ".package2", ".package3", "_Galleri", UnplayableFolderName, RotationUnknownFolderName,
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

            // Free local pass before spending a Claude call: most personal
            // photos needing an orientation fix contain a person, and the
            // face detector (already running locally for IndexFacesAsync)
            // only reliably finds faces when the image is actually upright.
            // Only trust a rotation that finds faces AND is the sole
            // rotation to do so - anything ambiguous (faces at multiple
            // angles, or none at all) falls through to Claude vision below
            // instead of risking a wrong guess.
            var faceDegrees = await TryDetectOrientationViaFacesAsync(file, cancellationToken);
            if (faceDegrees is { } fd)
            {
                decisions[hash] = fd;
                ApplyResolvedFile(file, fd, ref rotated);
                checkedPaths.Add(Path.GetRelativePath(libraryPath, file));
                toCheck++;
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

    // How many images get checked against Claude in one AnalyzeImageAsync
    // round of concurrency - same reasoning as FixOrientationAsync's
    // RotationConcurrency, just not batched into a single prompt since
    // AnalyzeImageAsync is a fixed one-image-per-call API (ImageTaggingService
    // uses the same shape for the same reason).
    private const int ScreenshotReclassifyConcurrency = 8;

    // Learned the hard way (2026-08-20, Rico's library): pixel-dimension
    // heuristics for "is this really a screenshot" are unreliable - real
    // screenshots and unrelated photos/drawings overlap heavily in size.
    // What actually works is Claude looking at whether real phone/app UI
    // chrome (status bar, nav buttons, app controls) is visible - which is
    // exactly what ClaudeImageAnalysisService's is_screenshot field already
    // reports, previously computed but never consumed for this purpose.
    public async Task<ScreenshotReclassifyResult> ReclassifyScreenshotsAsync(string libraryPath, int maxFiles = 500, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(libraryPath)) return new ScreenshotReclassifyResult();

        var screenshotsFolder = new[] { "Screenshots", "Skærmbilleder" }
            .Select(f => Path.Combine(libraryPath, f))
            .FirstOrDefault(Directory.Exists);
        if (screenshotsFolder is null) return new ScreenshotReclassifyResult();

        var imagesFolder = new[] { "Images", "Billeder" }
            .Select(f => Path.Combine(libraryPath, f))
            .FirstOrDefault(Directory.Exists)
            ?? Path.Combine(libraryPath, "Billeder");
        var andetDir = Path.Combine(imagesFolder, "Andet", "FraSkærmbilleder");

        var allFiles = Directory.EnumerateFiles(screenshotsFolder, "*", SearchOption.TopDirectoryOnly)
            .Where(MediaTypeHelper.IsImage)
            .ToList();
        var toCheck = allFiles.Take(maxFiles).ToList();
        var remaining = allFiles.Count - toCheck.Count;

        var kept = 0; var movedOut = 0; var failed = 0;
        var moveLock = new object();

        await Parallel.ForEachAsync(
            toCheck,
            new ParallelOptions { MaxDegreeOfParallelism = ScreenshotReclassifyConcurrency, CancellationToken = cancellationToken },
            async (file, ct) =>
            {
                try
                {
                    var result = await _imageAnalysis.AnalyzeImageAsync(file);
                    if (result.IsScreenshot)
                    {
                        Interlocked.Increment(ref kept);
                        return;
                    }

                    lock (moveLock)
                    {
                        Directory.CreateDirectory(andetDir);
                        var dest = Path.Combine(andetDir, Path.GetFileName(file));
                        var i = 2;
                        while (File.Exists(dest))
                        {
                            dest = Path.Combine(andetDir, $"{Path.GetFileNameWithoutExtension(file)}_{i}{Path.GetExtension(file)}");
                            i++;
                        }
                        File.Move(file, dest);
                    }
                    Interlocked.Increment(ref movedOut);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Screenshot reclassification failed for {Path}", file);
                    Interlocked.Increment(ref failed);
                }
            });

        _logger.LogInformation(
            "Screenshot reclassification complete for {LibraryPath}: {Kept} kept, {MovedOut} moved out, {Failed} failed, {Remaining} remaining (capped at {Cap}/run)",
            libraryPath, kept, movedOut, failed, remaining, maxFiles);

        return new ScreenshotReclassifyResult
        {
            Checked          = toCheck.Count,
            KeptAsScreenshot = kept,
            MovedOut         = movedOut,
            Failed           = failed,
            RemainingOverCap = Math.Max(0, remaining),
        };
    }

    public async Task<ScreenshotReclassifyResult> FindScreenshotsInImagesAsync(string sourceFolder, string destScreenshotsFolder, int maxFiles, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(sourceFolder)) return new ScreenshotReclassifyResult();

        var allFiles = Directory.EnumerateFiles(sourceFolder, "*", SearchOption.TopDirectoryOnly)
            .Where(MediaTypeHelper.IsImage)
            .ToList();
        var toCheck = allFiles.Take(maxFiles).ToList();
        var remaining = allFiles.Count - toCheck.Count;

        var kept = 0; var movedOut = 0; var failed = 0;
        var moveLock = new object();

        await Parallel.ForEachAsync(
            toCheck,
            new ParallelOptions { MaxDegreeOfParallelism = ScreenshotReclassifyConcurrency, CancellationToken = cancellationToken },
            async (file, ct) =>
            {
                try
                {
                    var result = await _imageAnalysis.AnalyzeImageAsync(file);
                    if (!result.IsScreenshot)
                    {
                        Interlocked.Increment(ref kept);
                        return;
                    }

                    lock (moveLock)
                    {
                        Directory.CreateDirectory(destScreenshotsFolder);
                        var dest = Path.Combine(destScreenshotsFolder, Path.GetFileName(file));
                        var i = 2;
                        while (File.Exists(dest))
                        {
                            dest = Path.Combine(destScreenshotsFolder, $"{Path.GetFileNameWithoutExtension(file)}_{i}{Path.GetExtension(file)}");
                            i++;
                        }
                        File.Move(file, dest);
                    }
                    Interlocked.Increment(ref movedOut);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Screenshot detection failed for {Path}", file);
                    Interlocked.Increment(ref failed);
                }
            });

        _logger.LogInformation(
            "Screenshot detection complete for {SourceFolder}: {Kept} kept as images, {MovedOut} moved to screenshots, {Failed} failed, {Remaining} remaining (capped at {Cap}/run)",
            sourceFolder, kept, movedOut, failed, remaining, maxFiles);

        return new ScreenshotReclassifyResult
        {
            Checked          = toCheck.Count,
            KeptAsScreenshot = kept,
            MovedOut         = movedOut,
            Failed           = failed,
            RemainingOverCap = Math.Max(0, remaining),
        };
    }

    // Same face-detection tier FixOrientationAsync tries first, exposed on
    // its own so it can run without ever touching the paid Claude fallback
    // ("should not cost a thing" - user, 2026-08-20). Whatever the free tier
    // can't confidently resolve is reported for manual review, not guessed
    // at or silently left as-is.
    public async Task<FreeOrientationFixResult> FixOrientationFreeOnlyAsync(string libraryPath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(libraryPath)) return new FreeOrientationFixResult();

        // Shares the SAME sidecar files as FixOrientationAsync (see that
        // method's own comments) - a file resolved by either the free or
        // paid tier is skipped by both from then on, and a duplicate of an
        // already-decided photo (same pre-rotation hash, e.g. a SmartFolders
        // copy) resolves for free instantly without a face-check at all.
        var checkedPathsFile = Path.Combine(libraryPath, RotationCheckedFileName);
        var decisionsFile = Path.Combine(libraryPath, RotationDecisionsFileName);

        var checkedPaths = LoadStringSet(checkedPathsFile);
        var decisions = LoadHashDecisions(decisionsFile);

        var images = EnumerateImagesForRotationCheck(libraryPath, cancellationToken).ToList();
        var unresolved = images
            .Where(f => !checkedPaths.Contains(Path.GetRelativePath(libraryPath, f)))
            .ToList();

        var checkedCount = 0;
        var rotated = 0;
        var needsReview = new List<string>();
        var sinceSave = 0;

        foreach (var file in unresolved)
        {
            cancellationToken.ThrowIfCancellationRequested();
            checkedCount++;

            var relativePath = Path.GetRelativePath(libraryPath, file);

            string? hash = null;
            try { hash = ComputeHash(file); } catch (IOException) { /* in use/vanished - fall through to a fresh check */ }

            if (hash is not null && decisions.TryGetValue(hash, out var cachedDegrees))
            {
                ApplyResolvedFile(file, cachedDegrees, ref rotated);
                checkedPaths.Add(relativePath);
            }
            else
            {
                var faceDegrees = await TryDetectOrientationViaFacesAsync(file, cancellationToken);
                if (faceDegrees is { } fd)
                {
                    if (hash is not null) decisions[hash] = fd;
                    ApplyResolvedFile(file, fd, ref rotated);
                    checkedPaths.Add(relativePath);
                }
                else
                {
                    // Quarantine rather than leave in place and re-try forever -
                    // this free-tier answer will never change on a re-run (same
                    // model, same photo). Moving it into RotationUnknownFolderName
                    // (now in RotationSkipFolders) means it's simply never
                    // enumerated by a future free-only run again - only an
                    // explicit paid FixOrientationAsync pass points at that
                    // folder specifically. No need to add it to checkedPaths -
                    // the folder-level skip already guarantees it.
                    var moved = MoveIntoCategoryFolder(libraryPath, file, RotationUnknownFolderName);
                    needsReview.Add(moved ?? relativePath);
                }
            }

            if (++sinceSave >= 500)
            {
                SaveStringSet(checkedPathsFile, checkedPaths);
                SaveHashDecisions(decisionsFile, decisions);
                sinceSave = 0;
            }
        }

        SaveStringSet(checkedPathsFile, checkedPaths);
        SaveHashDecisions(decisionsFile, decisions);

        _logger.LogInformation(
            "Free-only orientation check complete for {LibraryPath}: {Checked} newly checked, {Rotated} rotated, {NeedsReview} need manual review, {AlreadyDone} already resolved from a prior run",
            libraryPath, checkedCount, rotated, needsReview.Count, images.Count - unresolved.Count);

        return new FreeOrientationFixResult
        {
            PhotosChecked     = checkedCount,
            PhotosRotated     = rotated,
            NeedsManualReview = needsReview,
        };
    }

    public async Task<RotationDetectionResult> DetectRotatedImagesAsync(string libraryPath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(libraryPath)) return new RotationDetectionResult();

        var images = EnumerateImagesForRotationCheck(libraryPath, cancellationToken).ToList();
        var checkedCount = 0;
        var rotatedImages = new System.Collections.Concurrent.ConcurrentBag<RotatedImageInfo>();
        var needsReview = new System.Collections.Concurrent.ConcurrentBag<string>();

        // A real library can be tens of thousands of images, and this check
        // does 4 rotation candidates x one ONNX face-detection call each -
        // sequential would take hours. FaceOnnxRecognitionService serializes
        // every call behind an internal lock, so sharing one instance across
        // workers wouldn't actually parallelize anything - each partition
        // gets its own independent instance via the factory instead (same
        // reasoning as AddAi's own Func<IFaceRecognitionService> comment).
        var degreeOfParallelism = Environment.ProcessorCount;
        var partitions = images
            .Select((file, index) => (file, index))
            .GroupBy(x => x.index % degreeOfParallelism)
            .Select(g => g.Select(x => x.file).ToList())
            .ToList();

        await Task.WhenAll(partitions.Select(async partition =>
        {
            var faceService = _faceRecognitionFactory();
            foreach (var file in partition)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Interlocked.Increment(ref checkedCount);

                var faceDegrees = await TryDetectOrientationViaFacesAsync(file, cancellationToken, faceService);
                if (faceDegrees is { } fd)
                {
                    if (fd != 0)
                    {
                        rotatedImages.Add(new RotatedImageInfo
                        {
                            RelativePath  = Path.GetRelativePath(libraryPath, file),
                            DegreesNeeded = fd,
                        });
                    }
                }
                else
                {
                    needsReview.Add(Path.GetRelativePath(libraryPath, file));
                }
            }
        }));

        _logger.LogInformation(
            "Rotation detection complete for {LibraryPath}: {Checked} checked, {Rotated} need rotation, {NeedsReview} need manual review",
            libraryPath, checkedCount, rotatedImages.Count, needsReview.Count);

        return new RotationDetectionResult
        {
            PhotosChecked     = checkedCount,
            RotatedImages     = rotatedImages.OrderBy(r => r.RelativePath).ToList(),
            NeedsManualReview = needsReview.OrderBy(r => r).ToList(),
        };
    }

    // Runs IDuplicateService's own exact+perceptual-hash logic against
    // whatever is actually on disk right now, bucketed by (Year, Month)
    // folder path the same way the original Package1 pass buckets by
    // (Year, Month) metadata - close enough once a library is already
    // sorted, and avoids re-deriving date metadata from scratch. Never
    // deletes anything - only reports groups, same convention as
    // DeduplicateFolderAsync ("caller's responsibility to have confirmed
    // with the user first").
    public async Task<NearDuplicateReport> FindDuplicatesInLibraryAsync(string libraryPath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(libraryPath)) return new NearDuplicateReport();

        var files = Directory.EnumerateFiles(libraryPath, "*", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(Path.GetDirectoryName(f) ?? "").Equals("thumbnails", StringComparison.OrdinalIgnoreCase))
            .Where(MediaTypeHelper.IsImage)
            .ToList();

        var mediaFiles = new List<MediaFile>();
        foreach (var f in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string hash;
            try { hash = ComputeHash(f); }
            catch (IOException) { continue; }

            var info = new FileInfo(f);
            // Real per-file capture timestamp (same MediaDateService Package1
            // itself uses) rather than a year-only placeholder - lets
            // BuildDuplicateGroupsAsync's exact-timestamp pass work correctly
            // against an already-sorted library, not just fresh imports.
            // Falls back to a non-reliable Jan-1-of-year guess (same as
            // before) only when no real date can be resolved, purely to keep
            // the near-duplicate pass's Year/Month bucketing populated.
            var dateResult = _mediaDateService.GetBestDate(new MediaDateRequest(f));
            var mediaFile = dateResult.Date is { } realDate
                ? new MediaFile(f, realDate, ITMartin.Media.Contracts.Contracts.Runtime.Enums.MediaType.Image, info.Length, isDateReliable: dateResult.IsReliable)
                : new MediaFile(f, new DateTime(ExtractYearFromPath(f, libraryPath) ?? 2000, 1, 1), ITMartin.Media.Contracts.Contracts.Runtime.Enums.MediaType.Image, info.Length, isDateReliable: false);
            mediaFile.SetHash(hash);
            mediaFiles.Add(mediaFile);
        }

        var groups = await _duplicateService.BuildDuplicateGroupsAsync(mediaFiles, cancellationToken);

        var reportGroups = groups.Select(g => new NearDuplicateGroupInfo
        {
            Kind = g.Hash.StartsWith("phash:") ? "near" : "exact",
            RelativePaths = g.Files.Select(f => Path.GetRelativePath(libraryPath, f.FullPath)).ToList(),
            TotalSizeBytes = g.TotalSizeBytes,
        }).ToList();

        _logger.LogInformation(
            "Duplicate scan complete for {LibraryPath}: {Files} files scanned, {Exact} exact groups, {Near} near-duplicate groups",
            libraryPath, mediaFiles.Count, reportGroups.Count(g => g.Kind == "exact"), reportGroups.Count(g => g.Kind == "near"));

        return new NearDuplicateReport
        {
            FilesScanned = mediaFiles.Count,
            ExactGroups  = reportGroups.Count(g => g.Kind == "exact"),
            NearGroups   = reportGroups.Count(g => g.Kind == "near"),
            Groups       = reportGroups,
        };
    }

    // Folder names this pass already trusts as-is - a file sitting here is
    // taken at its word rather than re-classified (SmartFolders copies, admin
    // folders, etc.).
    private static readonly HashSet<string> ClassifySkipFolders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".package1", ".package2", ".package3", "_Galleri", "SmartFolders",
            UnplayableFolderName, RotationUnknownFolderName, "thumbnails", "working", "enhanced", "manifests", "temp",
            "Duplikater", "SlettesKandidater", "Ikke_identificeret",
        };

    private const int ClassifyAiConcurrency = 8;

    // Same canonical-codec allowlist MediaRulesWorkflowStep uses at
    // fresh-import time - kept as its own copy here since this pass works
    // directly off files on disk, not shared machinery.
    private static readonly HashSet<string> WebSafeVideoCodecsForNormalizedCheck =
        new(StringComparer.OrdinalIgnoreCase) { "h264", "hevc" };

    // Categories that are actually date-organized (Year/Month folders) -
    // matches FileStatusWorkflowStep's own list.
    private static readonly HashSet<string> DateOrganizedCategories =
        new(StringComparer.OrdinalIgnoreCase) { "Billeder", "Videoer" };

    private static List<string> BuildApplicableFlags(bool isImage, bool isVideo, string currentFolder)
    {
        var applicable = new List<string>
        {
            StepFlags.FileIsReadable, StepFlags.CategoryIsSet, StepFlags.SubCategoryIsSet,
            StepFlags.NotDuplicate, StepFlags.IsNormalized,
        };
        if (DateOrganizedCategories.Contains(currentFolder)) applicable.Add(StepFlags.DateIsSet);
        // RotationIsCorrect deliberately NOT added here - it only applies once
        // a file's category resolves to real photos (Billeder), added at that
        // point below. A screenshot/chat/meme was never rotated by a camera,
        // so checking its orientation is meaningless.
        if (isImage) applicable.Add(StepFlags.QualityChecked);
        return applicable;
    }

    // Re-derives the same canonical-format rule MediaRulesWorkflowStep uses
    // at fresh-import time (jpg/mp4-h264-or-hevc/mp3/pdf), so an
    // already-sorted library gets a faithful, not invented, answer.
    private (bool Normalized, string? Suggestion) CheckNormalized(string file, bool isImage, bool isVideo, bool isAudio)
    {
        var ext = Path.GetExtension(file).ToLowerInvariant();
        if (isImage)
            return ext is ".jpg" or ".jpeg"
                ? (true, null)
                : (false, $"{ext} is not the canonical image format (jpg) - needs conversion");

        if (isVideo)
        {
            if (ext != ".mp4") return (false, $"{ext} is not the canonical video container (mp4) - needs conversion");
            string? codec;
            try { codec = _videoMetadata.GetVideoCodec(file); }
            catch (Exception) { codec = null; }
            var webSafe = codec is not null && WebSafeVideoCodecsForNormalizedCheck.Contains(codec);
            return webSafe ? (true, null) : (false, $"Codec '{codec ?? "unknown"}' isn't web-safe - needs re-encoding");
        }

        if (isAudio)
            return ext == ".mp3" ? (true, null) : (false, $"{ext} is not the canonical audio format (mp3) - needs conversion");

        return ext == ".pdf" ? (true, null) : (false, $"{ext} is not the canonical document format (pdf) - needs conversion");
    }

    // Runs every applicable step-flag against whatever isn't already IsDone
    // in the registry - the real, unified answer to "run all of FileSorter's
    // steps against an already-sorted library, and shrink the pool every
    // time". Cheapest-first per file: registry fast-path (no hash) -> decode
    // check -> hash-keyed duplicate/done check -> free per-type checks
    // (format, date, camera-EXIF category, free rotation via local face
    // detection) -> AI vision only for whatever's still ambiguous (capped at
    // maxAiCalls, same call also answers QualityChecked at no extra cost).
    // RotationIsCorrect's PAID escalation is deliberately NOT part of this
    // pass - that stays FixOrientationAsync's own explicit, cost-gated call;
    // here a face-detection miss just leaves RotationIsCorrect false with a
    // suggestion pointing at that endpoint.
    // Hard ceiling on how many photos get the expensive free rotation check
    // (4x decode+ONNX each) in ONE call - the actual fix for a call that used
    // to take 7-12+ hours against a large backlog. Anything past this cap
    // just stays unresolved for the next call to pick up (registry already
    // saves at the end of every call, so nothing is lost) - combined with
    // RunUntilConvergedAsync, the backlog shrinks over several fast, bounded
    // rounds instead of one unbounded one. Same "always a real cap in code"
    // convention as MaxRotationChecksPerRun (FixOrientationAsync) and
    // maxAiCalls - never optional, never something to forget to set.
    private const int DefaultMaxRotationChecksPerRun = 1000;

    // Hard ceiling on how many not-yet-done files the SEQUENTIAL scan phase
    // touches in one call - found 2026-08-24 running this against mie: the
    // rotation cap alone wasn't enough, since the scan phase (hash + EXIF +
    // video metadata) is itself uncapped and spawns one ffprobe process per
    // video for creation-time - a video-heavy backlog can dominate a call's
    // wall-clock time before rotation-checking is ever reached. Once hit,
    // the scan just stops - remaining files are left completely untouched
    // (not upserted, not counted) for a future call to pick up, same
    // "shrinks every round" convergence as the rotation cap.
    private const int DefaultMaxFilesScannedPerRun = 3000;

    public async Task<FileStatusReport> RunAllStepsAsync(
        string libraryPath, int maxAiCalls, int? maxRotationParallelism = null, bool includeSlowSteps = true,
        int? maxRotationChecksPerRun = null, int? maxFilesScannedPerRun = null, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(libraryPath))
            return new FileStatusReport();

        var overallStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var registry = await _fileStatusRegistry.LoadAsync(libraryPath, cancellationToken);
        _logger.LogInformation("RunAllSteps[{LibraryPath}] registry load: {Elapsed}", libraryPath, overallStopwatch.Elapsed);

        var doneByPath = registry.Values
            .Where(r => r.IsDone)
            .GroupBy(r => r.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var allFiles = EnumerateForClassification(libraryPath, cancellationToken).ToList();
        var aiQueue = new List<(string File, string Hash, string RelativePath, FileInfo Info, Dictionary<string, FlagState> Partial, List<string> Applicable)>();
        var rotationQueue = new List<(string File, string Hash, string RelativePath, string Category, FileInfo Info, Dictionary<string, FlagState> Flags, List<string> Applicable)>();
        var rotatedCount = 0;
        var changed = false;
        var checkedSinceSave = 0;
        var effectiveScanCap = Math.Max(0, maxFilesScannedPerRun ?? DefaultMaxFilesScannedPerRun);
        var scannedThisRun = 0;
        var scanCapHit = false;

        foreach (var file in allFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(libraryPath, file);

            FileInfo info;
            try { info = new FileInfo(file); }
            catch (IOException) { continue; }

            if (doneByPath.TryGetValue(relativePath, out var knownDone) &&
                knownDone.SizeBytes == info.Length &&
                knownDone.LastWriteUtc == info.LastWriteTimeUtc)
            {
                continue; // same file, same spot, already fully done - no hash needed
            }

            if (scannedThisRun >= effectiveScanCap)
            {
                scanCapHit = true;
                break; // rest of allFiles left completely untouched - picked up next call
            }
            scannedThisRun++;

            var isImage = ITMartin.Media.Infrastructure.Media.MediaTypeHelper.IsImage(file);
            var isVideo = ITMartin.Media.Infrastructure.Media.MediaTypeHelper.IsVideo(file);
            var isAudio = ITMartin.Media.Infrastructure.Media.MediaTypeHelper.IsAudio(file);
            var isDocument = ITMartin.Media.Infrastructure.Media.MediaTypeHelper.IsDocument(file);
            if (!isImage && !isVideo && !isAudio && !isDocument) continue; // unrecognized type - out of scope here

            string hash;
            try { hash = ComputeHash(file); }
            catch (IOException) { continue; }

            if (registry.TryGetValue(hash, out var existingByHash) && existingByHash.IsDone)
                continue; // resolved under this hash already (e.g. seen at another path)

            if (++checkedSinceSave >= 1000)
            {
                await _fileStatusRegistry.SaveAsync(libraryPath, registry, cancellationToken);
                checkedSinceSave = 0;
            }

            var currentFolder = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
            var applicable = BuildApplicableFlags(isImage, isVideo, currentFolder);
            var flags = new Dictionary<string, FlagState>();

            // --- FileIsReadable ---
            bool readable;
            string? unreadableReason = null;
            if (isImage)
            {
                try { var identified = SixLabors.ImageSharp.Image.Identify(file); readable = identified is not null; if (!readable) unreadableReason = "Could not identify image format"; }
                catch (Exception ex) { readable = false; unreadableReason = $"Image failed to decode ({ex.GetType().Name})"; }
            }
            else
            {
                readable = info.Length > 0;
                if (!readable) unreadableReason = "File is 0 bytes";
            }
            flags[StepFlags.FileIsReadable] = new FlagState { Value = readable, Suggestion = unreadableReason };

            if (!readable)
            {
                UpsertRecord(registry, hash, relativePath, currentFolder, applicable, flags, info);
                changed = true;
                continue;
            }

            flags[StepFlags.CategoryIsSet] = new FlagState { Value = true };

            var isDup = registry.TryGetValue(hash, out var dupRecord) && !string.Equals(dupRecord.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase);
            flags[StepFlags.NotDuplicate] = new FlagState { Value = !isDup, Suggestion = isDup ? $"Exact duplicate of {dupRecord!.RelativePath}" : null };

            var (normalized, normSuggestion) = CheckNormalized(file, isImage, isVideo, isAudio);
            flags[StepFlags.IsNormalized] = new FlagState { Value = normalized, Suggestion = normSuggestion };

            if (applicable.Contains(StepFlags.DateIsSet))
            {
                var dateResult = _mediaDateService.GetBestDate(new MediaDateRequest(file));
                var hasYearFolder = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Skip(1).FirstOrDefault() is { } seg && seg.Length == 4 && int.TryParse(seg, out _);
                var dateOk = dateResult.IsReliable || hasYearFolder;
                flags[StepFlags.DateIsSet] = new FlagState { Value = dateOk, Suggestion = dateOk ? null : "No reliable date source (EXIF/GPS/face-match) found" };
            }

            var category = currentFolder;

            if (isImage)
            {
                var ambiguousFolder = currentFolder.Equals("Billeder", StringComparison.OrdinalIgnoreCase) ||
                                       currentFolder.Equals("Images", StringComparison.OrdinalIgnoreCase);

                var queuedForAi = false;
                var resolvedAsPhoto = false;
                if (!ambiguousFolder)
                {
                    // Already sitting in a folder that names an unambiguous
                    // category (Skærmbilleder/Chat/Memes/LivePhotos) - trust it.
                    // Never a real camera photo, so no rotation check applies.
                    flags[StepFlags.SubCategoryIsSet] = new FlagState { Value = true };
                    flags[StepFlags.QualityChecked] = new FlagState { Value = true };
                }
                else
                {
                    (string? Make, string? Model, string? Software)? meta;
                    try { meta = _exifService.ReadMetadata(file); } catch (Exception) { meta = null; }
                    var hasCameraExif = !string.IsNullOrWhiteSpace(meta?.Make) || !string.IsNullOrWhiteSpace(meta?.Model);

                    if (hasCameraExif)
                    {
                        // Real camera EXIF is the strongest free signal there
                        // is - screenshots/chat/memes categorically never
                        // carry it. No AI call made, so quality isn't
                        // independently verified - not treated as an open
                        // problem for a confirmed real photo, just unchecked.
                        category = "Billeder";
                        resolvedAsPhoto = true;
                        flags[StepFlags.SubCategoryIsSet] = new FlagState { Value = true };
                        flags[StepFlags.QualityChecked] = new FlagState { Value = true };
                    }
                    else
                    {
                        // Ambiguous - only the AI tier can tell Photo/Screenshot/
                        // Chat/Meme apart from here. Queue with whatever's
                        // already resolved so far; SubCategoryIsSet/QualityChecked/
                        // rotation get filled in after the AI batch below, once
                        // the real category (and whether rotation even applies)
                        // is known.
                        queuedForAi = true;
                        aiQueue.Add((file, hash, relativePath, info, flags, applicable));
                    }
                }

                // Rotation - free tier only (local face detection, no cost),
                // and only for confirmed real photos - a screenshot/chat/meme
                // was never rotated by a camera, so this doesn't apply to them.
                // Deferred to a parallel pass below instead of run inline here -
                // each check is 4x ONNX face-detection calls per photo, and
                // sequential was the actual bottleneck on a real library.
                var queuedForRotation = false;
                if (resolvedAsPhoto)
                {
                    applicable.Add(StepFlags.RotationIsCorrect);
                    queuedForRotation = true;
                    rotationQueue.Add((file, hash, relativePath, category, info, flags, applicable));
                }

                if (queuedForAi || queuedForRotation) { changed = true; continue; } // Upsert happens after the deferred pass(es) below
            }
            else
            {
                // Video/Audio/Document - trust the existing sub-classification,
                // nothing ambiguous enough here to warrant AI.
                flags[StepFlags.SubCategoryIsSet] = new FlagState { Value = true };
            }

            UpsertRecord(registry, hash, relativePath, category, applicable, flags, info);
            changed = true;
        }

        _logger.LogInformation(
            "RunAllSteps[{LibraryPath}] sequential scan: {Elapsed}, {Scanned} of {Total} files scanned this run{Capped} ({RotationQueued} queued for rotation, {AiQueued} queued for AI)",
            libraryPath, overallStopwatch.Elapsed, scannedThisRun, allFiles.Count,
            scanCapHit ? $" (capped at {effectiveScanCap} - rest left for a future run)" : "",
            rotationQueue.Count, aiQueue.Count);

        if (rotationQueue.Count > 0)
        {
            // Rotation-check-via-faces disabled here: TryDetectOrientationViaFacesAsync
            // only resolves a rotation when a face is found at EXACTLY ONE of the 4
            // trial rotations. Any photo with no detectable face (most non-portrait
            // photos) or with a face found at more than one rotation (routine
            // detector ambiguity) came back "unresolved" - and unresolved used to mean
            // quarantined into RotationUkendt, permanently, even though the vast
            // majority of those photos were already correctly oriented. Confirmed by
            // hand-checking 100 quarantined files from C:\mie\RotationUkendt: none
            // actually needed rotating, several had obvious faces yet still landed
            // there. Leaving rotation unresolved-but-in-place (same as the old
            // !includeSlowSteps fast path) until a real fix exists is safer than
            // guessing.
            foreach (var item in rotationQueue)
                UpsertRecord(registry, item.Hash, item.RelativePath, item.Category, item.Applicable, item.Flags, item.Info);
            changed = true;
        }

        var aiStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var aiCallsUsed = 0;
        if (aiQueue.Count > 0 && !includeSlowSteps)
        {
            // Fast pass: leave SubCategoryIsSet/QualityChecked (and
            // RotationIsCorrect, added once the real category is known) out
            // of Flags entirely - file stays wherever it already sits and
            // not-done, resolved by a later includeSlowSteps=true run.
            foreach (var item in aiQueue)
                UpsertRecord(registry, item.Hash, item.RelativePath, "Billeder", item.Applicable, item.Partial, item.Info);
            changed = true;
        }
        else if (aiQueue.Count > 0 && _anthropicClient is not null)
        {
            var toCheck = aiQueue.Take(Math.Max(0, maxAiCalls)).ToList();
            var moveLock = new object();

            await Parallel.ForEachAsync(
                toCheck,
                new ParallelOptions { MaxDegreeOfParallelism = ClassifyAiConcurrency, CancellationToken = cancellationToken },
                async (item, ct) =>
                {
                    AiAnalysisResult result;
                    try { result = await _imageAnalysis.AnalyzeImageAsync(item.File); }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Classify AI check failed for {Path}", item.File);
                        return;
                    }

                    var resolvedCategory =
                        result.IsChat ? "Chat" :
                        result.IsMeme ? "Memes" :
                        result.IsScreenshot ? "Skærmbilleder" :
                        "Billeder";

                    var qualityOk = !result.IsBlurry && !result.IsSolidColor;

                    // Rotation only applies once we know it's a real photo -
                    // run before taking the lock (slow, no shared state needed).
                    int? faceDegrees = null;
                    if (resolvedCategory == "Billeder")
                        faceDegrees = await TryDetectOrientationViaFacesAsync(item.File, ct);

                    var finalRelativePath = item.RelativePath;
                    var finalInfo = item.Info;
                    var rotatedHere = 0;

                    lock (moveLock)
                    {
                        if (resolvedCategory != "Billeder")
                        {
                            finalRelativePath = MoveIntoCategoryFolder(libraryPath, item.File, resolvedCategory) ?? item.RelativePath;
                            try { finalInfo = new FileInfo(Path.Combine(libraryPath, finalRelativePath)); } catch (IOException) { }
                        }
                        else if (faceDegrees is { } fd)
                        {
                            ApplyResolvedFile(item.File, fd, ref rotatedHere);
                            try { finalInfo = new FileInfo(item.File); } catch (IOException) { }
                        }

                        item.Partial[StepFlags.SubCategoryIsSet] = new FlagState { Value = true };
                        item.Partial[StepFlags.QualityChecked] = new FlagState
                        {
                            Value = qualityOk,
                            Suggestion = qualityOk ? null : result.IsBlurry ? "Image appears blurry" : "Image appears to be a solid color/blank",
                        };
                        if (resolvedCategory == "Billeder")
                        {
                            item.Applicable.Add(StepFlags.RotationIsCorrect);
                            item.Partial[StepFlags.RotationIsCorrect] = faceDegrees is not null
                                ? new FlagState { Value = true }
                                : new FlagState { Value = false, Suggestion = "No face detected at any rotation - run the paid rotation-fix pass or review manually" };
                        }

                        UpsertRecord(registry, item.Hash, finalRelativePath, resolvedCategory, item.Applicable, item.Partial, finalInfo);
                    }

                    Interlocked.Add(ref rotatedCount, rotatedHere);
                    Interlocked.Increment(ref aiCallsUsed);
                });

            changed = true;

            foreach (var skipped in aiQueue.Skip(toCheck.Count))
            {
                skipped.Partial[StepFlags.SubCategoryIsSet] = new FlagState { Value = false, Suggestion = "Ambiguous (no camera EXIF) - needs the AI classification tier, over this run's cap" };
                skipped.Partial[StepFlags.QualityChecked] = new FlagState { Value = false, Suggestion = "Not checked yet - same AI call as SubCategoryIsSet" };
                UpsertRecord(registry, skipped.Hash, skipped.RelativePath, "Billeder", skipped.Applicable, skipped.Partial, skipped.Info);
            }
        }
        else
        {
            foreach (var item in aiQueue)
            {
                item.Partial[StepFlags.SubCategoryIsSet] = new FlagState { Value = false, Suggestion = "Ambiguous (no camera EXIF) - needs the AI classification tier (none available this run)" };
                item.Partial[StepFlags.QualityChecked] = new FlagState { Value = false, Suggestion = "Not checked yet - same AI call as SubCategoryIsSet" };
                UpsertRecord(registry, item.Hash, item.RelativePath, "Billeder", item.Applicable, item.Partial, item.Info);
            }
            if (aiQueue.Count > 0) changed = true;
        }

        if (aiQueue.Count > 0)
            _logger.LogInformation("RunAllSteps[{LibraryPath}] AI classification phase: {Elapsed} for {Used} calls", libraryPath, aiStopwatch.Elapsed, aiCallsUsed);

        var saveStopwatch = System.Diagnostics.Stopwatch.StartNew();
        if (changed)
            await _fileStatusRegistry.SaveAsync(libraryPath, registry, cancellationToken);
        _logger.LogInformation("RunAllSteps[{LibraryPath}] final registry save: {Elapsed}", libraryPath, saveStopwatch.Elapsed);

        _logger.LogInformation(
            "Run-all-steps complete for {LibraryPath}: {Total} files walked, {Rotated} rotated for free, {AiCalls} AI checks used (capped at {Cap}), total elapsed {TotalElapsed}",
            libraryPath, allFiles.Count, rotatedCount, aiCallsUsed, maxAiCalls, overallStopwatch.Elapsed);

        return _fileStatusRegistry.BuildReport(registry);
    }

    public Task<FileStatusReport> RunUntilConvergedAsync(
        string libraryPath, int maxAiCallsPerIteration, int? maxRotationParallelism = null,
        int maxIterations = 10, CancellationToken cancellationToken = default) =>
        ConvergenceLoop.RunAsync(
            () => RunAllStepsAsync(libraryPath, maxAiCallsPerIteration, maxRotationParallelism, includeSlowSteps: true, cancellationToken: cancellationToken),
            maxIterations,
            cancellationToken);

    private static void UpsertRecord(
        Dictionary<string, FileStatusRecord> registry, string hash, string relativePath, string category,
        List<string> applicableFlags, Dictionary<string, FlagState> flags, FileInfo info)
    {
        registry[hash] = new FileStatusRecord
        {
            ContentHash = hash,
            RelativePath = relativePath,
            Category = category,
            ApplicableFlags = applicableFlags,
            Flags = flags,
            SizeBytes = info.Length,
            LastWriteUtc = info.LastWriteTimeUtc,
            LastUpdatedUtc = DateTimeOffset.UtcNow,
        };
    }

    // Physically relocates a misclassified file into its resolved category
    // folder (created alongside the library's other top-level categories),
    // collision-safe. Returns the new relative path, or null if the move
    // failed (file left where it was, flagged NeedsManualReview by the caller
    // implicitly falling through to the next run).
    private string? MoveIntoCategoryFolder(string libraryPath, string currentFullPath, string category)
    {
        try
        {
            var destDir = Path.Combine(libraryPath, category);
            Directory.CreateDirectory(destDir);
            var destPath = ResolveNameCollision(Path.Combine(destDir, Path.GetFileName(currentFullPath)));
            File.Move(currentFullPath, destPath);
            return Path.GetRelativePath(libraryPath, destPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to move {Path} into {Category}", currentFullPath, category);
            return null;
        }
    }

    private IEnumerable<string> EnumerateForClassification(string directory, CancellationToken cancellationToken)
    {
        foreach (var file in Directory.EnumerateFiles(directory))
            yield return file;

        foreach (var subDir in Directory.EnumerateDirectories(directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ClassifySkipFolders.Contains(Path.GetFileName(subDir))) continue;

            foreach (var file in EnumerateForClassification(subDir, cancellationToken))
                yield return file;
        }
    }

    // "BurstN" subfolders aren't produced by any current pipeline step (see
    // feedback re: 2026-08-22 cleanup) - this just undoes the grouping:
    // move every file back up to the parent folder, remove the now-empty
    // BurstN directory. Matches only a literal "Burst" + digits folder name,
    // never touches anything else.
    private static readonly System.Text.RegularExpressions.Regex BurstFolderPattern =
        new(@"^Burst\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    public Task<BurstFlattenResult> FlattenBurstFoldersAsync(string libraryPath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(libraryPath))
            return Task.FromResult(new BurstFlattenResult());

        var foldersFlattened = 0;
        var filesMoved = 0;

        foreach (var burstDir in Directory.EnumerateDirectories(libraryPath, "*", SearchOption.AllDirectories)
                     .Where(d => BurstFolderPattern.IsMatch(Path.GetFileName(d)))
                     .ToList())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parent = Path.GetDirectoryName(burstDir)!;

            foreach (var file in Directory.EnumerateFiles(burstDir, "*", SearchOption.AllDirectories).ToList())
            {
                try
                {
                    var destPath = ResolveNameCollision(Path.Combine(parent, Path.GetFileName(file)));
                    File.Move(file, destPath);
                    filesMoved++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to flatten {Path} out of its Burst folder", file);
                }
            }

            try
            {
                Directory.Delete(burstDir, recursive: true);
                foldersFlattened++;
            }
            catch (IOException) { /* left-over subfolder/file that failed to move above - skip */ }
        }

        _logger.LogInformation(
            "Burst-folder flatten complete for {LibraryPath}: {Folders} folders flattened, {Files} files moved back to their parent",
            libraryPath, foldersFlattened, filesMoved);

        return Task.FromResult(new BurstFlattenResult { FoldersFlattened = foldersFlattened, FilesMoved = filesMoved });
    }

    // Windows Media Player/Zune's own cache naming - a GUID is never a
    // coincidence, so this pattern alone is safe to move automatically.
    private static readonly System.Text.RegularExpressions.Regex AlbumArtCachePattern =
        new(@"^AlbumArt[ _]?[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}[ _]Large(_\d+)?$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    // Same exact-name list Package1's MediaRulesWorkflowStep uses, plus
    // "endswith" matching (catches "VA - Absolute Music 69 - front.jpg",
    // "Erkenntnis Theorietapecover.jpg") - broader than Package1's check
    // since there's no audio-sibling signal left to lean on post-sort, so
    // these are review-only, never auto-moved.
    private static readonly string[] AlbumArtNameHints =
        ["cover", "folder", "albumart", "albumartsmall", "albumartlarge", "front", "back"];

    public Task<AlbumArtReclassifyResult> ReclassifyAlbumArtAsync(string libraryPath, CancellationToken cancellationToken = default)
    {
        var imagesFolder = new[] { "Images", "Billeder" }
            .Select(f => Path.Combine(libraryPath, f))
            .FirstOrDefault(Directory.Exists);
        if (imagesFolder is null) return Task.FromResult(new AlbumArtReclassifyResult());

        var musikFolder = new[] { "Musik", "Music" }
            .Select(f => Path.Combine(libraryPath, f))
            .FirstOrDefault(Directory.Exists)
            ?? Path.Combine(libraryPath, "Musik");
        var albumArtDir = Path.Combine(musikFolder, "AlbumArt");

        var checkedCount = 0;
        var moved = 0;
        var reviewCandidates = new List<string>();

        foreach (var file in Directory.EnumerateFiles(imagesFolder, "*", SearchOption.AllDirectories)
                     .Where(MediaTypeHelper.IsImage)
                     .ToList())
        {
            cancellationToken.ThrowIfCancellationRequested();
            checkedCount++;

            var nameLower = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();

            if (AlbumArtCachePattern.IsMatch(Path.GetFileNameWithoutExtension(file)))
            {
                try
                {
                    Directory.CreateDirectory(albumArtDir);
                    var targetPath = ResolveNameCollision(Path.Combine(albumArtDir, Path.GetFileName(file)));
                    File.Move(file, targetPath);
                    moved++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to move album-art cache file {Path} to Musik", file);
                }
                continue;
            }

            // Strip a trailing collision suffix ("_2", " 2", "(2)") before
            // matching - found necessary 2026-08-25 on mie's real library,
            // where repeated imports left sequences like Folder_2..Folder_8.jpg
            // that the plain EndsWith check missed entirely.
            var nameWithoutCollisionSuffix =
                System.Text.RegularExpressions.Regex.Replace(nameLower, @"[ _]?\(?\d+\)?$", "");

            if (AlbumArtNameHints.Any(hint => nameWithoutCollisionSuffix.EndsWith(hint, StringComparison.Ordinal)))
            {
                reviewCandidates.Add(file);
            }
        }

        _logger.LogInformation(
            "Album-art reclassify complete for {LibraryPath}: {Checked} checked, {Moved} moved (cache-pattern), {Review} flagged for manual review",
            libraryPath, checkedCount, moved, reviewCandidates.Count);

        return Task.FromResult(new AlbumArtReclassifyResult
        {
            Checked = checkedCount,
            MovedHighConfidence = moved,
            ReviewCandidates = reviewCandidates,
        });
    }

    // Real digital-camera/phone photos essentially never fall under this size
    // even at low quality settings; web-scraped/scanned CD art routinely
    // does. Deliberately conservative (a genuine low-res old photo can be
    // this small too) - paired with the no-EXIF check below so a folder only
    // gets flagged when BOTH signals agree.
    private const long SmallFileSizeThresholdBytes = 60_000;
    private const double SuspectFolderMinFraction = 0.8;
    private const int SuspectFolderMinFileCount = 5;

    public Task<NonPhotoClusterReport> FindNonPhotoClustersAsync(string libraryPath, CancellationToken cancellationToken = default)
    {
        var imagesFolder = new[] { "Images", "Billeder" }
            .Select(f => Path.Combine(libraryPath, f))
            .FirstOrDefault(Directory.Exists);
        if (imagesFolder is null) return Task.FromResult(new NonPhotoClusterReport());

        var foldersScanned = 0;
        var suspects = new List<SuspectFolder>();

        foreach (var dir in Directory.EnumerateDirectories(imagesFolder, "*", SearchOption.AllDirectories)
                     .Append(imagesFolder)
                     .Where(d => !Path.GetFileName(d).Equals("thumbnails", StringComparison.OrdinalIgnoreCase)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var files = Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly)
                .Where(MediaTypeHelper.IsImage)
                .ToList();
            if (files.Count < SuspectFolderMinFileCount) continue;

            foldersScanned++;

            var smallNoExifCount = 0;
            long totalSize = 0;
            foreach (var file in files)
            {
                long size;
                try { size = new FileInfo(file).Length; }
                catch (Exception) { continue; }
                totalSize += size;

                // .gif is never real camera/phone output - counts on its own,
                // no need for the size/EXIF checks below.
                if (Path.GetExtension(file).Equals(".gif", StringComparison.OrdinalIgnoreCase))
                {
                    smallNoExifCount++;
                    continue;
                }

                (int? Width, int? Height) dimensions;
                try { dimensions = _exifService.GetDimensions(file); }
                catch (Exception) { dimensions = (null, null); }
                var maxDimension = dimensions.Width.HasValue && dimensions.Height.HasValue
                    ? Math.Max(dimensions.Width.Value, dimensions.Height.Value)
                    : (int?)null;

                // A real camera photo can be small on disk if heavily
                // compressed, but it still decodes to full resolution - this
                // ceiling keeps that case out regardless of file size.
                if (maxDimension is >= 1600) continue;

                // Small on disk OR low native resolution - either is
                // consistent with web-scraped/scanned art, real cameras
                // (even old ones) don't produce low-res output.
                var looksLikeArtSize = size <= SmallFileSizeThresholdBytes || maxDimension is <= 800;
                if (!looksLikeArtSize) continue;

                (string? Make, string? Model, string? Software)? meta;
                try { meta = _exifService.ReadMetadata(file); }
                catch (Exception) { meta = null; }

                var hasCameraExif = !string.IsNullOrWhiteSpace(meta?.Make) || !string.IsNullOrWhiteSpace(meta?.Model);
                if (!hasCameraExif) smallNoExifCount++;
            }

            if (smallNoExifCount < files.Count * SuspectFolderMinFraction) continue;

            suspects.Add(new SuspectFolder
            {
                FolderPath = dir,
                FileCount = files.Count,
                NoExifCount = smallNoExifCount,
                AvgFileSizeBytes = totalSize / files.Count,
                SampleFileNames = files.Take(5).Select(Path.GetFileName).ToList()!,
            });
        }

        _logger.LogInformation(
            "Non-photo cluster scan complete for {LibraryPath}: {Scanned} folders scanned, {Suspects} flagged",
            libraryPath, foldersScanned, suspects.Count);

        return Task.FromResult(new NonPhotoClusterReport { FoldersScanned = foldersScanned, SuspectFolders = suspects });
    }

    // Best-effort: looks for a 4-digit year as a whole path segment - fine
    // for scoping the perceptual-hash comparison buckets, not meant to be
    // authoritative metadata.
    private static int? ExtractYearFromPath(string filePath, string libraryPath)
    {
        var rel = Path.GetRelativePath(libraryPath, filePath);
        foreach (var segment in rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (segment.Length == 4 && int.TryParse(segment, out var year) && year is > 1990 and < 2100)
                return year;
        }
        return null;
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

    // Tries every rotation locally via the face detector before falling back
    // to a paid Claude vision call. Writes each candidate rotation to a temp
    // file (never mutates the real one until a decision is made) and picks
    // the single rotation that found faces, if exactly one did.
    private async Task<int?> TryDetectOrientationViaFacesAsync(string filePath, CancellationToken cancellationToken, IFaceRecognitionService? faceService = null)
    {
        faceService ??= _faceRecognitionService;
        var tempDir = Path.Combine(Path.GetTempPath(), $"rotcheck_{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempDir);
            var faceCounts = new Dictionary<int, int>();

            foreach (var degrees in new[] { 0, 90, 180, 270 })
            {
                cancellationToken.ThrowIfCancellationRequested();

                var tempPath = Path.Combine(tempDir, $"{degrees}{Path.GetExtension(filePath)}");

                try
                {
                    var mode = degrees switch
                    {
                        90 => RotateMode.Rotate90,
                        180 => RotateMode.Rotate180,
                        270 => RotateMode.Rotate270,
                        _ => RotateMode.None,
                    };

                    using (var image = Image.Load(filePath))
                    {
                        if (mode != RotateMode.None) image.Mutate(x => x.Rotate(mode));
                        image.Save(tempPath);
                    }

                    var faces = await faceService.ExtractFaceEmbeddingsAsync(tempPath);
                    faceCounts[degrees] = faces.Count;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    faceCounts[degrees] = 0;
                }
            }

            var withFaces = faceCounts.Where(kv => kv.Value > 0).ToList();
            return withFaces.Count == 1 ? withFaces[0].Key : null;
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort cleanup */ }
        }
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
