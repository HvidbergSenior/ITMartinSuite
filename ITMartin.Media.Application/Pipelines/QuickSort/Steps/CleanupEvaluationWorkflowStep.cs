using ITMartin.Media.Application.Pipelines.QuickSort.Models;
using ITMartin.Media.Application.Pipelines.QuickSort.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Contracts.Entities;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.QuickSort.Steps;

public sealed class CleanupEvaluationWorkflowStep
    : QuickSortWorkflowStepBase
{
    // Duration, not size - confirmed 2026-09-06 against Rico/AC's archive
    // that size alone can't separate the two: real "Batman Brave and the
    // Bold" episode rips are ~230-244MB each, while several genuinely
    // personal camera clips in the same library run 462MB-1GB (long
    // continuous recordings, just high-bitrate). A downloaded episode/movie
    // is essentially always 20+ minutes; a personal clip is almost always a
    // few minutes regardless of file size.
    private static readonly TimeSpan LargeFilmDurationThreshold = TimeSpan.FromMinutes(20);

    // Matches LibraryPolishService.PruneSmallAlbumsAsync's existing default,
    // but counts across an artist's ENTIRE catalog (all albums), not per
    // album - confirmed 2026-09-06.
    private const int MinSongsPerArtist = 6;

    private readonly QuickSortCleanupResultBuilder
        _cleanupResultBuilder;

    private readonly ILogger<
            CleanupEvaluationWorkflowStep>
        _logger;

    public CleanupEvaluationWorkflowStep(
        QuickSortCleanupResultBuilder cleanupResultBuilder,
        ILogger<CleanupEvaluationWorkflowStep> logger)
    {
        _cleanupResultBuilder =
            cleanupResultBuilder;

        _logger =
            logger;
    }

    public override string Name =>
        "Cleanup";

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        var state =
            context.State as QuickSortWorkflowState
            ?? throw new InvalidOperationException(
                "Invalid workflow state");

        cancellationToken
            .ThrowIfCancellationRequested();

        if (state.CleanupResult is not null)
        {
            _logger.LogInformation(
                "Cleanup already completed");

            return;
        }

        await ExecuteOperationAsync(
            "CleanupEvaluation",
            $"Files={state.MediaFiles.Count}",
            async () =>
            {
                var total =
                    state.MediaFiles.Count;

                var current = 0;

                foreach (var mediaFile in state.MediaFiles)
                {
                    current++;

                    LogStepProgress(
                        _logger,
                        Name,
                        current,
                        total,
                        mediaFile.FileName);

                    mediaFile.Status =
                        MediaFileStatus.ToKeep;

                    mediaFile.CleanupDecision =
                        CleanupDecision.Keep;
                }

                foreach (var group in state.DuplicateGroups)
                {
                    var keep =
                        group.Files
                            .OrderByDescending(x => x.SizeBytes)
                            .First();

                    keep.Status =
                        MediaFileStatus.ToKeep;

                    keep.CleanupDecision =
                        CleanupDecision.Keep;

                    foreach (var duplicate in group.Files
                                 .Where(x => x != keep))
                    {
                        duplicate.Status =
                            MediaFileStatus.ToDelete;

                        duplicate.CleanupDecision =
                            CleanupDecision.Delete;

                        duplicate.ExportSubFolder =
                            "Duplicates";
                    }
                }

                foreach (var mediaFile in state.MediaFiles
                             .Where(f => f.ExportSubFolder != "Duplicates"))
                {
                    if (IsDeleteCandidate(mediaFile))
                        mediaFile.ExportSubFolder = "DeleteCandidates";
                }

                // Downloaded movies/TV rips confirmed 2026-09-06 on Rico/AC's
                // archive (a full "Batman Brave and the Bold" episode set) -
                // ClassifyVideoSubCategory's filename heuristics (SxxExx,
                // rip-source keywords) miss these because the files aren't
                // named like a rip. Duration catches them regardless of name
                // or file size (see LargeFilmDurationThreshold above for why
                // size alone doesn't work here). Files whose duration
                // couldn't be read (HasValue false - corrupt/unsupported)
                // are left alone rather than guessed at.
                foreach (var mediaFile in state.MediaFiles
                             .Where(f => f.ExportSubFolder is not ("Duplicates" or "DeleteCandidates" or "Unplayable")))
                {
                    if (mediaFile.Type == MediaType.Video &&
                        mediaFile.Duration is { } duration &&
                        duration > LargeFilmDurationThreshold)
                        mediaFile.ExportSubFolder = "LargeFilm";
                }

                // An artist with only a handful of tracks scattered across the
                // library (a single downloaded song, a stray sample) isn't a
                // real album worth keeping in Musik proper - confirmed
                // 2026-09-06. Counts across ALL of that artist's albums, not
                // per-album, since a real artist can have several small
                // albums that individually look sparse. Files with no Artist
                // tag are left alone entirely - they can't be reliably
                // attributed to "one artist with too few songs" (Metadata's
                // Artist read failed for each independently, not because they
                // share an artist).
                var audioByArtist = state.MediaFiles
                    .Where(f => f.MainCategory == MediaMainCategory.Audio &&
                                f.ExportSubFolder is not ("Duplicates" or "DeleteCandidates" or "LargeFilm") &&
                                !string.IsNullOrWhiteSpace(f.Artist))
                    .GroupBy(f => f.Artist!.Trim(), StringComparer.OrdinalIgnoreCase);

                foreach (var group in audioByArtist)
                {
                    if (group.Count() > MinSongsPerArtist) continue;

                    foreach (var mediaFile in group)
                        mediaFile.ExportSubFolder = "SmallArtist";
                }

                var result =
                    _cleanupResultBuilder.Run(
                        state.MediaFiles);

                state.CleanupResult =
                    result;

                _logger.LogInformation(
                    """
                    Cleanup completed
                    Keep: {Keep}
                    Delete: {Delete}
                    """,
                    result.KeepCount,
                    result.DeleteCount);

                await Task.CompletedTask;
            },
            _logger);
    }

    private static bool IsDeleteCandidate(MediaFile file)
    {
        // Video too short to be meaningful
        if (file.Duration.HasValue && file.Duration.Value.TotalSeconds < 3)
            return true;

        // Tiny image — likely icon, thumbnail, or web asset
        if (file.Width.HasValue && file.Height.HasValue &&
            file.Width.Value < 150 && file.Height.Value < 150)
            return true;

        return false;
    }
}