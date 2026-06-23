using ITMartin.Media.Contracts.Contracts.Runtime.Helpers;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Persistence;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

public sealed class ThumbnailWorkflowStep
    : Package1WorkflowStepBase
{
    private readonly IThumbnailService
        _thumbnailService;

    private readonly ILogger<
            ThumbnailWorkflowStep>
        _logger;

    private readonly IWorkflowInstanceStore
        _workflowInstanceStore;

    public ThumbnailWorkflowStep(
        IThumbnailService thumbnailService,
        ILogger<ThumbnailWorkflowStep> logger,
        IWorkflowInstanceStore workflowInstanceStore)
    {
        _thumbnailService =
            thumbnailService;

        _logger =
            logger;

        _workflowInstanceStore =
            workflowInstanceStore;
    }

    public override string Name =>
        "Thumbnails";

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
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
                continue;
            }

            processed++;

            LogStepProgress(
                _logger,
                Name,
                processed,
                total,
                file.FileName);

            if (processed % 10 == 0 || processed == total)
            {
                await _workflowInstanceStore.SetProgressAsync(
                    context.WorkflowId,
                    processed,
                    total,
                    item: file.FileName,
                    cancellationToken: cancellationToken);
            }

            var thumbnailSource =
                file.NormalizedPath
                ?? file.FullPath;

            if (!_thumbnailService.Supports(
                    thumbnailSource))
            {
                continue;
            }

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

            var ok = await ExecuteOperationAsync(
                "GenerateThumbnail",
                file.FileName,
                async () =>
                {
                    file.ThumbnailPath =
                        await _thumbnailService
                            .GenerateAsync(
                                thumbnailSource,
                                thumbnailPath,
                                cancellationToken);
                },
                _logger);

            if (!ok)
                state.FailedFiles.Add(new FailedFile { FilePath = file.FullPath, Step = Name, Error = "Thumbnail generation failed" });
        }
    }
}