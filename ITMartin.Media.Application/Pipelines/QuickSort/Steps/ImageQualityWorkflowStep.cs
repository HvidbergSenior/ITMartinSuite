using ITMartin.Media.Application.Pipelines.QuickSort.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.QuickSort.Steps;

// Free local blur/solid-color check, run on every image - see
// IImageQualityService. Lets QualityChecked resolve without ever needing the
// paid AiClassificationWorkflowStep, the same way the free ONNX rotation
// tier lets RotationIsCorrect resolve without the paid Claude fallback.
public sealed class ImageQualityWorkflowStep : QuickSortWorkflowStepBase
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
        var state = context.State as QuickSortWorkflowState
            ?? throw new InvalidOperationException("Invalid workflow state");

        var files = state.MediaFiles.Where(x => x.IsImage).ToList();
        var total = files.Count;
        var current = 0;

        // Each file is only ever touched by its own task (IsBlurry/IsSolidColor
        // are per-file, IImageQualityService is stateless), so this is safe to
        // parallelize the same way DuplicateService's perceptual-hash pass was -
        // one image at a time took ~50 hours projected on a 12,767-image
        // library. `current` is the only shared state, hence Interlocked.
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount),
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(files, parallelOptions, async (file, ct) =>
        {
            var itemNumber = Interlocked.Increment(ref current);
            LogStepProgress(_logger, Name, itemNumber, total, file.FileName);

            var path = file.NormalizedPath ?? file.FullPath;
            if (!File.Exists(path))
            {
                return;
            }

            await ExecuteOperationAsync(
                "CheckImageQuality",
                file.FileName,
                async () =>
                {
                    var (isBlurry, isSolidColor) = await _imageQuality.AnalyzeAsync(path, ct);
                    file.IsBlurry = isBlurry;
                    file.IsSolidColor = isSolidColor;
                },
                _logger);
        });
    }
}
