using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

public sealed class VideoNormalizationWorkflowStep
    : Package1WorkflowStepBase
{
    private readonly IVideoBatchService
        _videoBatchService;

    private readonly ILogger<
            VideoNormalizationWorkflowStep>
        _logger;

    public VideoNormalizationWorkflowStep(
        IVideoBatchService videoBatchService,
        ILogger<VideoNormalizationWorkflowStep> logger)
    {
        _videoBatchService =
            videoBatchService;

        _logger =
            logger;
    }

    public override string Name =>
        "VideoNormalization";

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        var state =
            context.State as Package1WorkflowState
            ?? throw new InvalidOperationException(
                "Invalid workflow state");

        var filesToNormalize =
            state.MediaFiles
                .Where(x =>
                    x.IsVideo &&
                    x.RequiresNormalization)
                .ToList();

        if (filesToNormalize.Count == 0)
        {
            _logger.LogInformation(
                "No videos require normalization");

            return;
        }

        await ExecuteOperationAsync(
            "NormalizeVideos",
            $"Videos={filesToNormalize.Count}",
            async () =>
            {
                await _videoBatchService
                    .ConvertAllVideosAsync(
                        filesToNormalize,
                        (current, total, message) =>
                        {
                            LogStepProgress(
                                _logger,
                                Name,
                                current,
                                total,
                                message);
                        },
                        cancellationToken);
            },
            _logger);
    }
}