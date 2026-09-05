using System.Linq;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.QuickSort.Steps;

// A Live Photo is two separate files on disk (e.g. IMG_1234.HEIC + IMG_1234.MOV)
// with no metadata linking them beyond a shared filename. The rest of the pipeline
// has no concept of the pair, so this runs once after per-file rules to flag the
// video half before export routing decides which folder it lands in.
//
// A same-name image+video pair alone isn't proof of an actual Apple Live Photo -
// confirmed 2026-09-03 on Rico's archive, where old camcorder clips (e.g. a
// 20-second .AVI) that happened to share a filename with an unrelated still got
// misclassified this way, including files from 2011 (Live Photos didn't exist
// before the iPhone 6s, September 2015). A real Live Photo's motion clip is
// always ~1.5-3 seconds, so anything longer than LivePhotoMaxDuration is real
// video content, not a Live Photo companion, regardless of filename match.
public sealed class LivePhotoDetectionWorkflowStep : QuickSortWorkflowStepBase
{
    private static readonly TimeSpan LivePhotoMaxDuration = TimeSpan.FromSeconds(5);

    private readonly IVideoMetadataService _videoMetadataService;
    private readonly ILogger<LivePhotoDetectionWorkflowStep> _logger;

    public LivePhotoDetectionWorkflowStep(
        IVideoMetadataService videoMetadataService,
        ILogger<LivePhotoDetectionWorkflowStep> logger)
    {
        _videoMetadataService = videoMetadataService;
        _logger = logger;
    }

    public override string Name => "LivePhotoDetection";

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        var state = context.State as QuickSortWorkflowState
            ?? throw new InvalidOperationException("Invalid workflow state");

        await ExecuteOperationAsync(
            "LivePhotoDetection",
            $"Files={state.MediaFiles.Count}",
            async () =>
            {
                var pairCount = 0;

                var groups = state.MediaFiles
                    .Where(f => f.Type == MediaType.Image || f.Type == MediaType.Video)
                    .GroupBy(f => Path.GetFileNameWithoutExtension(f.FileName).ToLowerInvariant());

                foreach (var group in groups)
                {
                    var images = group.Where(f => f.Type == MediaType.Image).ToList();
                    var videos = group.Where(f => f.Type == MediaType.Video).ToList();

                    if (images.Count == 1 && videos.Count == 1)
                    {
                        var duration = _videoMetadataService.GetDuration(videos[0].FullPath);
                        if (duration is not null && duration.Value > LivePhotoMaxDuration)
                        {
                            _logger.LogInformation(
                                "Skipping Live Photo pairing for {File}: video is {Duration}, longer than a real Live Photo clip",
                                videos[0].FileName, duration.Value);
                            continue;
                        }

                        videos[0].SubCategory = MediaSubCategory.LivePhotoVideo;
                        pairCount++;
                    }
                }

                _logger.LogInformation("Live Photo pairs detected: {Count}", pairCount);

                await Task.CompletedTask;
            },
            _logger);
    }
}
