using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

// Free local blur/solid-color check, run on every image - see
// IImageQualityService. Lets QualityChecked resolve without ever needing the
// paid AiClassificationWorkflowStep, the same way the free ONNX rotation
// tier lets RotationIsCorrect resolve without the paid Claude fallback.
public sealed class ImageQualityWorkflowStep : Package1WorkflowStepBase
{
    private readonly IImageQualityService _imageQuality;
    private readonly ILogger<ImageQualityWorkflowStep> _logger;

    public ImageQualityWorkflowStep(
        IImageQualityService imageQuality,
        ILogger<ImageQualityWorkflowStep> logger)
    {
        _imageQuality = imageQuality;
        _logger = logger;
    }

    public override string Name => "ImageQuality";

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        var state = context.State as Package1WorkflowState
            ?? throw new InvalidOperationException("Invalid workflow state");

        var files = state.MediaFiles.Where(x => x.IsImage).ToList();
        var total = files.Count;
        var current = 0;

        foreach (var file in files)
        {
            current++;
            cancellationToken.ThrowIfCancellationRequested();

            LogStepProgress(_logger, Name, current, total, file.FileName);

            var path = file.NormalizedPath ?? file.FullPath;
            if (!File.Exists(path))
            {
                continue;
            }

            await ExecuteOperationAsync(
                "CheckImageQuality",
                file.FileName,
                async () =>
                {
                    var (isBlurry, isSolidColor) = await _imageQuality.AnalyzeAsync(path, cancellationToken);
                    file.IsBlurry = isBlurry;
                    file.IsSolidColor = isSolidColor;
                },
                _logger);
        }
    }
}
