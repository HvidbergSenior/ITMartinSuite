using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Domain.Steps.NormalizationStep;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

public sealed class VideoNormalizationWorkflowStep
    : IWorkflowStep
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

    public string Name => "VideoNormalization";

    public async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
    {
        var state =
            context.State as Package1WorkflowState
            ?? throw new InvalidOperationException(
                "Invalid workflow state");

        _logger.LogInformation(
            "Starting video normalization");

        await _videoBatchService
            .ConvertAllVideosAsync(
                state.MediaFiles,
                (current, total, message) =>
                {
                    _logger.LogInformation(
                        "{Current}/{Total} {Message}",
                        current,
                        total,
                        message);
                });

        _logger.LogInformation(
            "Video normalization completed");
    }
}