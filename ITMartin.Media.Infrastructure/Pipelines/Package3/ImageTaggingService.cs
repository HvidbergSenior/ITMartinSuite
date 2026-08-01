using System.Text.Json;
using ITMartin.Media.Application.Pipelines.Package1.Services;
using ITMartin.Media.Application.Pipelines.Package2.Services;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Infrastructure.Pipelines.Package3;

public sealed class ImageTaggingService : IImageTaggingService
{
    private readonly IImageAnalysisService _imageAnalysis;
    // Constructed directly rather than DI-injected - Package1ManifestLoader/Writer
    // are only registered via AddPackage1Pipeline (the Worker's DI graph), not
    // FileSorter.Server's, and both have parameterless constructors anyway so
    // there's no reason to depend on a registration that may not be present in
    // whichever host runs this service.
    private readonly Package1ManifestLoader _manifestLoader = new();
    private readonly Package1ManifestWriter _manifestWriter = new();
    private readonly ILogger<ImageTaggingService> _logger;

    public ImageTaggingService(
        IImageAnalysisService imageAnalysis,
        ILogger<ImageTaggingService> logger)
    {
        _imageAnalysis = imageAnalysis;
        _logger = logger;
    }

    // A real library can be thousands of photos - tagged one at a time this
    // runs for hours. Bounded concurrency (a handful of Claude calls in flight
    // at once, not one-by-one) cuts that down substantially while staying well
    // under normal API rate limits.
    private const int MaxConcurrency = 8;

    // Saving only at the very end would mean a crash/restart partway through
    // loses everything already paid for and re-does it from scratch, so
    // progress is flushed to manifest.json periodically instead - a restart
    // only ever re-does the last partial batch.
    private const int SaveEveryNPhotos = 25;

    // Hard ceiling on real API calls per invocation - see CLAUDE.md "AI/Claude
    // API cost discipline". Incremental-skip protects re-runs from re-costing
    // the whole library, but does nothing to protect a single first run
    // against an unexpectedly huge untagged count (e.g. a customer with
    // 50,000 photos) - this cap is what actually bounds that. A library with
    // more untagged photos than the cap needs multiple clicks, on purpose.
    private const int MaxCallsPerRun = 500;

    public async Task<ImageTaggingResult> TagLibraryAsync(string libraryPath, CancellationToken cancellationToken = default)
    {
        // Loaded twice deliberately. `manifest` is rebased to local absolute
        // paths (Package1ManifestLoader does this automatically whenever
        // libraryPath differs from the manifest's own stored RootPath) so
        // files can actually be read from disk on whichever machine runs
        // this. `rawManifest` keeps ExportedPath in its original form exactly
        // as it was on disk - that's the copy that gets written back out.
        // Persisting the rebased (local-machine) paths instead would bake in
        // a path that only resolves on the machine that ran the tagging pass,
        // silently breaking every photo lookup once the library is synced
        // back to wherever it's actually served from (this happened for real
        // the first time this ran - see feedback_ai_cost_ceiling memory).
        var manifest = await _manifestLoader.LoadAsync(libraryPath, cancellationToken);

        var manifestPath = Path.Combine(libraryPath, "manifest.json");
        var rawJson = await File.ReadAllTextAsync(manifestPath, cancellationToken);
        var rawManifest = JsonSerializer.Deserialize<Package1Manifest>(rawJson)
            ?? throw new InvalidOperationException($"Failed to deserialize {manifestPath}");
        var rawById = rawManifest.MediaFiles.ToDictionary(f => f.Id);

        // Already-tagged files are skipped, not re-analyzed - safe to run this
        // again and again (including after new photos have been added), since
        // a re-run only ever costs whatever's new, never the whole library again.
        var allUntagged = manifest.MediaFiles
            .Where(f => f.IsImage && f.AiTags.Count == 0 && f.ExportedPath is not null && File.Exists(f.ExportedPath))
            .ToList();
        var untagged = allUntagged.Take(MaxCallsPerRun).ToList();
        var remaining = allUntagged.Count - untagged.Count;

        var totalImages     = manifest.MediaFiles.Count(f => f.IsImage);
        var alreadyTagged   = totalImages - allUntagged.Count;
        var tagged          = 0;
        var taggedSinceSave = 0;
        var saveLock        = new SemaphoreSlim(1, 1);

        async Task CheckpointIfDueAsync(CancellationToken ct)
        {
            if (Volatile.Read(ref taggedSinceSave) < SaveEveryNPhotos) return;

            await saveLock.WaitAsync(ct);
            try
            {
                // Re-check inside the lock - another concurrent caller may
                // have already saved and reset the counter while we waited.
                if (taggedSinceSave >= SaveEveryNPhotos)
                {
                    await _manifestWriter.WriteAsync(libraryPath, rawManifest, ct);
                    taggedSinceSave = 0;
                    _logger.LogInformation("Image tagging progress for {LibraryPath}: {Tagged}/{Total} tagged so far", libraryPath, tagged, untagged.Count);
                }
            }
            finally
            {
                saveLock.Release();
            }
        }

        await Parallel.ForEachAsync(
            untagged,
            new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrency, CancellationToken = cancellationToken },
            async (file, ct) =>
            {
                try
                {
                    var result = await _imageAnalysis.AnalyzeImageAsync(file.ExportedPath!);
                    if (result.Tags.Count > 0)
                    {
                        file.AiTags = result.Tags;
                        if (rawById.TryGetValue(file.Id, out var rawFile))
                            rawFile.AiTags = result.Tags;

                        Interlocked.Increment(ref tagged);
                        Interlocked.Increment(ref taggedSinceSave);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Tagging failed for {Path}", file.ExportedPath);
                }

                await CheckpointIfDueAsync(ct);
            });

        if (taggedSinceSave > 0)
        {
            await _manifestWriter.WriteAsync(libraryPath, rawManifest, cancellationToken);
        }

        _logger.LogInformation(
            "Image tagging complete for {LibraryPath}: {Tagged} newly tagged, {AlreadyTagged} already tagged, {Remaining} remaining (capped at {Cap}/run), {Total} total images",
            libraryPath, tagged, alreadyTagged, remaining, MaxCallsPerRun, totalImages);

        return new ImageTaggingResult
        {
            TaggedCount        = tagged,
            AlreadyTaggedCount = alreadyTagged,
            TotalImages        = totalImages,
            RemainingCount     = remaining,
        };
    }
}
