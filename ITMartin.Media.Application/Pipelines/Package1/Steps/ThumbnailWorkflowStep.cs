using ITMartin.Media.Contracts.Contracts.Runtime.Helpers;
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

    public string Name =>
        "Thumbnails";

    public async Task ExecuteAsync<TState>(
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
            cancellationToken
                .ThrowIfCancellationRequested();

            if (MediaTypeHelper.IsVideo(file.FullPath))
            {
                _logger.LogInformation(
                    "Skipping video thumbnail for {File}",
                    file.FullPath);

                continue;
            }

            var thumbnailSource =
                file.NormalizedPath
                ?? file.FullPath;

            _logger.LogInformation(
                "Generating thumbnail for {File}",
                thumbnailSource);

            var thumbnailDirectory =
                Path.Combine(
                    Path.GetDirectoryName(
                        thumbnailSource)!,
                    "thumbnails");

            Directory.CreateDirectory(
                thumbnailDirectory);

            var thumbnailPath =
                Path.Combine(
                    thumbnailDirectory,
                    $"{Path.GetFileNameWithoutExtension(thumbnailSource)}.jpg");

            file.ThumbnailPath =
                await _thumbnailService
                    .GenerateAsync(
                        thumbnailSource,
                        thumbnailPath,
                        cancellationToken);

            processed++;

            _logger.LogInformation(
                "Generated thumbnails {Processed}/{Total}",
                processed,
                total);
        }
    }
}