using System.Linq;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.QuickSort.Steps;

// A Live Photo is two separate files on disk (e.g. IMG_1234.HEIC + IMG_1234.MOV)
// with no metadata linking them beyond a shared filename. The rest of the pipeline
// has no concept of the pair, so this runs once after per-file rules to flag the
// video half before export routing decides which folder it lands in.
public sealed class LivePhotoDetectionWorkflowStep : QuickSortWorkflowStepBase
{
    private readonly ILogger<LivePhotoDetectionWorkflowStep> _logger;

    public LivePhotoDetectionWorkflowStep(ILogger<LivePhotoDetectionWorkflowStep> logger)
    {
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
