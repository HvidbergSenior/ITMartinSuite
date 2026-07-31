using System.Linq;
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

    // No dedicated Meme category - filename-only heuristics can't reliably tell a
    // meme from a personal photo shared the same way (both come through as
    // received_/fb_img_-style names from messaging apps). Unclassified images
    // fall through to OtherImage -> the regular Images category instead.

    private static readonly string[] ScreenRecordingKeywords =
        ["screenrecord", "screen record", "skærmoptagelse"];

    // iPhone/iPad/Android screenshots are named IMG_XXXX with no keyword, so they can only be
    // caught by exact device resolution. This runs later, from MetadataWorkflowStep, once
    // Width/Height are known — ClassifyImageSubCategory below only has the filename to go on.
    private static readonly (int W, int H)[] ScreenshotResolutions =
    [
        (1920, 1080), (1080, 1920),
        (2560, 1440), (1440, 2560),
        (2560, 1600), (1600, 2560),
        (3840, 2160), (2160, 3840),
        (1280, 800),  (800, 1280),
        (1366, 768),  (768, 1366),
        (2732, 2048), (2048, 2732), // iPad
        (1170, 2532), (2532, 1170), // iPhone 12/13
        (1284, 2778), (2778, 1284), // iPhone Pro Max
        (1080, 2340), (2340, 1080), // Android common
        (1080, 2400), (2400, 1080),
    ];

    public static bool IsScreenshotResolution(int? width, int? height)
    {
        if (width is null || height is null) return false;
        return ScreenshotResolutions.Contains((width.Value, height.Value));
    }

    private static void ClassifyImageSubCategory(MediaFile mediaFile)
    {
        if (mediaFile.Type != MediaType.Image) return;

        var nameLower = Path.GetFileNameWithoutExtension(mediaFile.FileName).ToLowerInvariant();

        if (ScreenshotKeywords.Any(k => nameLower.Contains(k)))
        {
            mediaFile.SubCategory = MediaSubCategory.Screenshot;
            return;
        }

        mediaFile.SubCategory = MediaSubCategory.OtherImage;
    }

    // Nothing previously set video SubCategory at all in the live pipeline -
    // every video silently stayed UnknownVideo (its constructor default)
    // regardless of whether it was a real screen recording or a phone video.
    private static void ClassifyVideoSubCategory(MediaFile mediaFile)
    {
        if (mediaFile.Type != MediaType.Video) return;

        var nameLower = Path.GetFileNameWithoutExtension(mediaFile.FileName).ToLowerInvariant();

        // iOS's real native screen-recording filename is "RPReplay_Final...",
        // not "screenrecord" - a filename-substring check for just
        // "screenrecord" misses it and misses "Screen Recording ..." (with a
        // space) too, since neither contains "screenrecord" as one word.
        if (ScreenRecordingKeywords.Any(k => nameLower.Contains(k)) ||
            nameLower.StartsWith("rpreplay"))
        {
            mediaFile.SubCategory = MediaSubCategory.ScreenRecording;
            return;
        }

        if (nameLower.StartsWith("vid_") || nameLower.StartsWith("mov_"))
        {
            mediaFile.SubCategory = MediaSubCategory.PhoneVideo;
            return;
        }

        mediaFile.SubCategory = MediaSubCategory.OtherVideo;
    }

    private void ApplyRules(
    MediaFile mediaFile)
{
    var extension =
        mediaFile.Extension
            .ToLowerInvariant();

    ClassifyImageSubCategory(mediaFile);
    ClassifyVideoSubCategory(mediaFile);

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