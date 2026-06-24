using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

public sealed class MediaRulesWorkflowStep
    : Package1WorkflowStepBase
{
    private readonly ILogger<
            MediaRulesWorkflowStep>
        _logger;

    public MediaRulesWorkflowStep(
        ILogger<MediaRulesWorkflowStep> logger)
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

    private static readonly string[] ScreenshotKeywords =
        ["screenshot", "screen shot", "screen_shot", "skærmbillede", "bildschirmfoto", "capture_", "captura"];

    private static readonly string[] MemeKeywords =
        ["fb_img_", "received_", "tumblr_", "meme", "funny_", "ifunny"];

    private static void ClassifyImageSubCategory(MediaFile mediaFile)
    {
        if (mediaFile.Type != MediaType.Image) return;

        var nameLower = Path.GetFileNameWithoutExtension(mediaFile.FileName).ToLowerInvariant();

        if (ScreenshotKeywords.Any(k => nameLower.Contains(k)))
        {
            mediaFile.SubCategory = MediaSubCategory.Screenshot;
            return;
        }

        if (MemeKeywords.Any(k => nameLower.Contains(k)))
        {
            mediaFile.SubCategory = MediaSubCategory.Meme;
            return;
        }

        mediaFile.SubCategory = MediaSubCategory.OtherImage;
    }

    private void ApplyRules(
    MediaFile mediaFile)
{
    var extension =
        mediaFile.Extension
            .ToLowerInvariant();

    ClassifyImageSubCategory(mediaFile);

    switch (extension)
    {
        // Canonical video format
        case ".mp4":

            mediaFile.IsNormalized = true;
            mediaFile.RequiresNormalization = false;
            mediaFile.RequiresEnhancement = true;

            break;

        // Non-canonical video formats
        case ".mkv":
        case ".avi":
        case ".mov":
        case ".wmv":
        case ".mts":
        case ".m2ts":

            mediaFile.IsNormalized = false;
            mediaFile.RequiresNormalization = true;
            mediaFile.RequiresEnhancement = false;

            break;

        // Canonical image format
        case ".jpg":

            mediaFile.IsNormalized = true;
            mediaFile.RequiresNormalization = false;

            break;

        // Non-canonical image formats
        case ".jpeg":
        case ".png":
        case ".bmp":
        case ".tiff":
        case ".webp":
        case ".heic":

            mediaFile.IsNormalized = false;
            mediaFile.RequiresNormalization = true;

            break;

        // Canonical audio format
        case ".mp3":

            mediaFile.IsNormalized = true;
            mediaFile.RequiresNormalization = false;

            break;

        // Non-canonical audio formats
        case ".wav":
        case ".flac":
        case ".aac":
        case ".m4a":

            mediaFile.IsNormalized = false;
            mediaFile.RequiresNormalization = true;

            break;

        // Canonical document format
        case ".pdf":

            mediaFile.IsNormalized = true;
            mediaFile.RequiresNormalization = false;

            break;

        // Non-canonical document formats
        case ".doc":
        case ".docx":
        case ".rtf":
        case ".odt":

            mediaFile.IsNormalized = false;
            mediaFile.RequiresNormalization = true;

            break;

        default:

            mediaFile.IsNormalized = false;
            mediaFile.RequiresNormalization = false;

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