// ThumbnailWorkflowStep.cs

using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

public sealed class ThumbnailWorkflowStep
    : IWorkflowStep
{
    private readonly IThumbnailService
        _thumbnailService;

    private readonly ILogger<
        ThumbnailWorkflowStep>
        _logger;

    public ThumbnailWorkflowStep(
        IThumbnailService thumbnailService,
        ILogger<ThumbnailWorkflowStep> logger)
    {
        _thumbnailService =
            thumbnailService;

        _logger =
            logger;
    }

    public string Name => "Thumbnails";

    public Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
    {
        _logger.LogInformation(
            "Executing {Step}",
            nameof(ThumbnailWorkflowStep));

        var state =
            context.State as Package1WorkflowState
            ?? throw new InvalidOperationException(
                "Invalid workflow state");

        var total =
            state.MediaFiles.Count;

        var processed = 0;

        foreach (var file in state.MediaFiles)
        {
            if (file.IsVideo)
            {
                _logger.LogInformation(
                    "Skipping video thumbnail for {File}",
                    file.FullPath);

                continue;
            }

            _logger.LogInformation(
                "Generating thumbnail for {File}",
                file.FullPath);

            file.ThumbnailPath =
                _thumbnailService
                    .GenerateThumbnail(file);

            processed++;

            _logger.LogInformation(
                "Generated thumbnails {Processed}/{Total}",
                processed,
                total);
        }

        return Task.CompletedTask;
    }
}