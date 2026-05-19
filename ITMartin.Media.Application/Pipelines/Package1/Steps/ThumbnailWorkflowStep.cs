// File:
// ITMartin.Media.Application/Pipelines/Package1/Steps/ThumbnailWorkflowStep.cs

using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Domain.Interfaces;
using ITMartin.Media.Interfaces;
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
        var state =
            context.State as Package1WorkflowState
            ?? throw new InvalidOperationException(
                "Invalid workflow state");

        foreach (var file in state.MediaFiles)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            if (!string.IsNullOrWhiteSpace(
                    file.ThumbnailPath))
            {
                continue;
            }

            _logger.LogInformation(
                "Generating thumbnail for {File}",
                file.FullPath);

            var thumbnail =
                _thumbnailService
                    .GenerateThumbnail(
                        file);

            if (thumbnail is null)
            {
                continue;
            }

            file.ThumbnailPath =
                thumbnail;

            _logger.LogInformation(
                "Generated thumbnails {Count}/{Total}",
                state.MediaFiles.Count(x =>
                    !string.IsNullOrWhiteSpace(
                        x.ThumbnailPath)),
                state.MediaFiles.Count);
        }

        return Task.CompletedTask;
    }
}