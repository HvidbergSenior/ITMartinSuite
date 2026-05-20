using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

public sealed class ImageNormalizationWorkflowStep
    : IWorkflowStep
{
    private readonly IImageConverterService
        _imageConverterService;

    private readonly ILogger<
        ImageNormalizationWorkflowStep>
        _logger;

    public ImageNormalizationWorkflowStep(
        IImageConverterService imageConverterService,
        ILogger<ImageNormalizationWorkflowStep> logger)
    {
        _imageConverterService =
            imageConverterService;

        _logger =
            logger;
    }

    public string Name => "ImageNormalization";

    public async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
    {
        _logger.LogInformation(
            "Executing {Step}",
            nameof(ImageNormalizationWorkflowStep));
        var state =
            context.State as Package1WorkflowState
            ?? throw new InvalidOperationException(
                "Invalid workflow state");

        foreach (var file in state.MediaFiles
                     .Where(x =>
                         x.Type == MediaType.Image))
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            if (!string.IsNullOrWhiteSpace(
                    file.NormalizedPath))
            {
                continue;
            }

            _logger.LogInformation(
                "Normalizing image {File}",
                file.FullPath);

            var normalized =
                await _imageConverterService
                    .ConvertToJpgAsync(
                        file.FullPath);

            file.NormalizedPath =
                normalized;
        }

        _logger.LogInformation(
            "Image normalization completed");
    }
}