using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

public sealed class MediaClassificationWorkflowStep
    : Package1WorkflowStepBase
{
    private readonly ILogger<
            MediaClassificationWorkflowStep>
        _logger;

    public MediaClassificationWorkflowStep(
        ILogger<MediaClassificationWorkflowStep> logger)
    {
        _logger =
            logger;
    }

    public override string Name =>
        "MediaClassification";

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

        var current = 0;

        foreach (var mediaFile in state.MediaFiles)
        {
            current++;

            LogStepProgress(
                _logger,
                Name,
                current,
                total,
                mediaFile.FileName);

            await ExecuteOperationAsync(
                "ClassifyMedia",
                mediaFile.FileName,
                async () =>
                {
                    ApplyRules(mediaFile);

                    await Task.CompletedTask;
                },
                _logger);
        }
    }

    private void ApplyRules(
        MediaFile mediaFile)
    {
        var extension =
            mediaFile.Extension
                .ToLowerInvariant();

        switch (extension)
        {
            case ".mp4":
            case ".mkv":

                mediaFile.IsNormalized = true;

                mediaFile.RequiresNormalization =
                    false;

                mediaFile.RequiresEnhancement =
                    true;

                break;

            case ".avi":
            case ".mov":
            case ".wmv":
            case ".mts":
            case ".m2ts":

                mediaFile.IsNormalized = false;

                mediaFile.RequiresNormalization =
                    true;

                mediaFile.RequiresEnhancement =
                    false;

                break;

            case ".jpg":
            case ".jpeg":

                mediaFile.IsNormalized = true;

                mediaFile.RequiresNormalization =
                    false;

                break;

            case ".png":
            case ".bmp":
            case ".tiff":
            case ".webp":
            case ".heic":

                mediaFile.IsNormalized = false;

                mediaFile.RequiresNormalization =
                    true;

                break;

            case ".mp3":
            case ".flac":
            case ".wav":

                mediaFile.RequiresNormalization =
                    false;

                break;

            case ".pdf":
            case ".doc":
            case ".docx":

                mediaFile.RequiresNormalization =
                    false;

                break;

            default:

                mediaFile.RequiresNormalization =
                    false;

                break;
        }

        _logger.LogInformation(
            """
            Classified:
            {File}
            Type={Type}
            Normalized={Normalized}
            RequiresNormalization={RequiresNormalization}
            RequiresEnhancement={RequiresEnhancement}
            """,
            mediaFile.FileName,
            mediaFile.Type,
            mediaFile.IsNormalized,
            mediaFile.RequiresNormalization,
            mediaFile.RequiresEnhancement);
    }
}