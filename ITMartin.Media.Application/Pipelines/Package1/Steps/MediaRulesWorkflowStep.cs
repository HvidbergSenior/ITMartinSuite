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
    private static readonly HashSet<string> WebSafeVideoCodecs =
        new(StringComparer.OrdinalIgnoreCase) { "h264", "hevc" };

    private readonly ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IVideoMetadataService
        _videoMetadataService;

    private readonly ILogger<
            MediaRulesWorkflowStep>
        _logger;

    public MediaRulesWorkflowStep(
        ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IVideoMetadataService videoMetadataService,
        ILogger<MediaRulesWorkflowStep> logger)
    {
        _videoMetadataService =
            videoMetadataService;

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

    // Exact-resolution matching was tried and confirmed broken on real data
    // (2026-07-31, Malene's library): a genuine screenshot exported at
    // 960x2079 matched no hardcoded device resolution, because any resize,
    // forward, or re-export through a messaging app changes the pixel
    // dimensions away from the device's native size - a losing game to chase
    // with more hardcoded resolutions. A phone's camera never outputs PNG;
    // real photos are JPG/HEIC, and PNG is screenshots/saved images/shared
    // graphics almost exclusively - format is the reliable signal here, not
    // exact pixel dimensions.
    private static void ClassifyImageSubCategory(MediaFile mediaFile)
    {
        if (mediaFile.Type != MediaType.Image) return;

        var nameLower = Path.GetFileNameWithoutExtension(mediaFile.FileName).ToLowerInvariant();

        if (ScreenshotKeywords.Any(k => nameLower.Contains(k)))
        {
            mediaFile.SubCategory = MediaSubCategory.Screenshot;
            return;
        }

        if (Path.GetExtension(mediaFile.FileName).Equals(".png", StringComparison.OrdinalIgnoreCase))
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
        // ".mp4" is just a container - the codec inside still needs checking.
        // Old point-and-shoots/camcorders often wrote MPEG-4 Part 2 or other
        // non-web-safe codecs into an .mp4 file, and no browser can play
        // those, so trusting the extension alone let them through unfixed.
        case ".mp4":

            var codec = _videoMetadataService.GetVideoCodec(mediaFile.FullPath);
            var webSafe = codec is not null && WebSafeVideoCodecs.Contains(codec);

            mediaFile.IsNormalized = webSafe;
            mediaFile.RequiresNormalization = !webSafe;
            mediaFile.RequiresEnhancement = webSafe;

            _logger.LogInformation(
                "Codec check for {File}: {Codec} (web-safe={WebSafe})",
                mediaFile.FileName, codec ?? "unknown", webSafe);

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