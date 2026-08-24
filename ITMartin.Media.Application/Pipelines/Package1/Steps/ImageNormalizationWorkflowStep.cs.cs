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

        // Every image, not just ones needing a format conversion -
        // ConvertToJpgAsync's own "keep original" path already does a cheap,
        // decode-free EXIF-tag check and only pays for a real decode+re-encode
        // when a photo actually needs rotating (see ImageConverterService).
        // Running it unconditionally is how a plain already-JPG photo (the
        // majority of real camera/phone photos) gets its orientation baked
        // and known at all - previously this step's RequiresNormalization
        // filter meant that never happened for them.
        var files =
            state.MediaFiles
                .Where(x => x.IsImage)
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

            var ok = await ExecuteOperationAsync(
                "NormalizeImage",
                file.FileName,
                async () =>
                {
                    file.OrientationKnownFromExif =
                        _imageConverterService.TryGetSourceOrientation(file.FullPath, out _);

                    if (!string.IsNullOrWhiteSpace(
                            file.NormalizedPath))
                    {
                        return;
                    }

                    file.NormalizedPath =
                        await _imageConverterService
                            .ConvertToJpgAsync(
                                file.FullPath);
                    file.IsNormalized = true;
                    _logger.LogInformation(
                        "Normalized {Source} -> {Output}",
                        file.FullPath,
                        file.NormalizedPath);
                },
                _logger);

            if (!ok)
                state.FailedFiles.Add(new FailedFile { FilePath = file.FullPath, Step = Name, Error = "Image normalization failed" });
        }
    }
}