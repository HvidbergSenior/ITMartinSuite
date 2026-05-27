using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

public sealed class ImageNormalizationWorkflowStep
    : Package1WorkflowStepBase
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

    public override string Name =>
        "ImageNormalization";

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        var state =
            context.State as Package1WorkflowState
            ?? throw new InvalidOperationException(
                "Invalid workflow state");

        var files =
            state.MediaFiles
                .Where(x =>
                    x.IsImage &&
                    x.RequiresNormalization)
                .ToList();

        var total =
            files.Count;

        var current = 0;

        foreach (var file in files)
        {
            current++;

            LogStepProgress(
                _logger,
                Name,
                current,
                total,
                file.FileName);

            await ExecuteOperationAsync(
                "NormalizeImage",
                file.FileName,
                async () =>
                {
                    if (!string.IsNullOrWhiteSpace(
                            file.NormalizedPath))
                    {
                        return;
                    }

                    file.NormalizedPath =
                        await _imageConverterService
                            .ConvertToJpgAsync(
                                file.FullPath);
                },
                _logger);
        }
    }
}