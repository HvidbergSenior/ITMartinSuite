using System.Linq;
using ITMartin.Media.Application.Pipelines.QuickSort.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.QuickSort.Steps;

public sealed class MediaRulesWorkflowStep
    : QuickSortWorkflowStepBase
{
    private static readonly HashSet<string> WebSafeVideoCodecs =
        new(StringComparer.OrdinalIgnoreCase) { "h264", "hevc" };

    private readonly ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IVideoMetadataService
        _videoMetadataService;

    private readonly ITMartin.Media.Contracts.Contracts.Runtime.Workflows.IConcurrentVideoDispatcher
        _videoDispatcher;

    private readonly ILogger<
            MediaRulesWorkflowStep>
        _logger;

    public MediaRulesWorkflowStep(
        ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IVideoMetadataService videoMetadataService,
        ITMartin.Media.Contracts.Contracts.Runtime.Workflows.IConcurrentVideoDispatcher videoDispatcher,
        ILogger<MediaRulesWorkflowStep> logger)
    {
        _videoMetadataService =
            videoMetadataService;

        _videoDispatcher =
            videoDispatcher;

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
            context.State as QuickSortWorkflowState
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

                    // Dispatch the conversion the moment we know it needs
                    // one, instead of QuickSort waiting for a dedicated
                    // normalization step later - it now races concurrently
                    // against everything below (hash, dedup, metadata,
                    // export...). See IConcurrentVideoDispatcher and
                    // VideoConvertFinalizeWorkflowStep.
                    if (mediaFile.IsVideo && mediaFile.RequiresNormalization)
                    {
                        _videoDispatcher.Dispatch(mediaFile, cancellationToken);
                    }

                    await Task.CompletedTask;
                },
                _logger);
        }

        // Embedded album/cover art (see LooksLikeAlbumArt) was previously
        // delivered into Musik on the theory it's real per-album content
        // worth keeping - confirmed 2026-09-03 on Rico/AC's archive that in
        // practice it's just app-generated cache clutter (iTunes' internal
        // artwork store, embedded cover.jpg/folder.jpg copies) the user
        // deleted on sight every time it appeared, including a review-folder
        // routing that still got rejected. Removed from MediaFiles entirely
        // here (before hashing/dedup/export ever run) rather than delivered
        // anywhere, including SlettesKandidater - it's never real content,
        // so there's nothing worth keeping a review copy of.
        state.MediaFiles.RemoveAll(f => f.SubCategory == MediaSubCategory.AlbumArt);
    }

    private static readonly string[] ScreenshotKeywords =
        ["screenshot", "screen shot", "screen_shot", "skærmbillede", "bildschirmfoto", "capture_", "captura"];

    // Meme/Chat exist as real categories (see MediaSubCategory), but filename
    // heuristics alone can't reliably tell them apart from a personal photo
    // shared the same way (both come through as received_/fb_img_-style names
    // from messaging apps) - that distinction is left to AiClassificationWorkflowStep's
    // AI tier (is_meme/is_chat). Unclassified images fall through to OtherImage
    // -> the regular Images category, same as before.

    private static readonly string[] ScreenRecordingKeywords =
        ["screenrecord", "screen record", "skærmoptagelse"];

    // Standard filenames media players/taggers use for embedded cover art
    // (Windows Media Player, iTunes, foobar2000, etc. all follow one of
    // these conventions). Requires the image to actually sit in a folder
    // with audio files too - a personal photo that happens to be named
    // "cover.jpg" (e.g. a scanned book cover) shouldn't get swept into Musik
    // just because of its name alone. Anything this misses (non-standard
    // names) is left for FaceIndex's manual triage.
    private static readonly string[] AlbumArtFileNames =
        ["cover", "folder", "albumart", "albumartsmall", "albumartlarge", "front", "back"];

    private static readonly string[] AudioExtensions =
        [".mp3", ".flac", ".wav", ".aac", ".m4a", ".wma", ".ogg"];

    private static bool LooksLikeAlbumArt(MediaFile mediaFile)
    {
        var nameLower = Path.GetFileNameWithoutExtension(mediaFile.FileName).ToLowerInvariant();

        // iTunes' own artwork cache (historically "Album Artwork\Cache" in
        // the iTunes Media folder) names each file "AlbumArt <GUID>
        // Large/Small.jpg" - one per album, sitting in a flat cache folder
        // with no audio files alongside it at all. Confirmed 2026-09-03 on
        // Rico/AC's archive: 114 of these landed in Billeder among real
        // family photos because the exact-stem check below only matches the
        // bare "albumartlarge"/"albumartsmall" names, not this GUID-embedded
        // variant. The naming pattern itself is unambiguous - no real photo
        // is ever named this way - so this doesn't need the audio-sibling
        // check the exact-name cases below still require.
        if (nameLower.StartsWith("albumart ", StringComparison.Ordinal) &&
            (nameLower.EndsWith(" large") || nameLower.EndsWith(" small")))
        {
            return true;
        }

        if (!AlbumArtFileNames.Contains(nameLower)) return false;

        var directory = Path.GetDirectoryName(mediaFile.FullPath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return false;

        return Directory.EnumerateFiles(directory)
            .Any(f => AudioExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));
    }

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

        if (LooksLikeAlbumArt(mediaFile))
        {
            mediaFile.SubCategory = MediaSubCategory.AlbumArt;
            return;
        }

        // No camera or phone produces an animated GIF - unlike PNG (which
        // still needs the AI tier to catch a real photo saved through a
        // lossless pipeline), format alone is 100% proof here. Confirmed
        // 2026-09-03 on Rico/AC's archive: every GIF found was a saved
        // meme/reaction image, none were personal photos.
        if (Path.GetExtension(mediaFile.FileName).Equals(".gif", StringComparison.OrdinalIgnoreCase))
        {
            mediaFile.SubCategory = MediaSubCategory.Gif;
            return;
        }

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

    // Downloaded/ripped TV episodes and movies, not personal footage - a
    // camera or phone never produces a filename with episode notation or a
    // rip-source/release-group tag. Confirmed 2026-09-03 on Rico/AC's
    // archive: New Girl, Spectacular Spider-Man, and a Batman animated
    // series were mixed in among genuinely personal MVI_/IMG_-style camera
    // videos, and several of these episode files had been false-positive
    // quarantined as "unplayable" (see CanDecodeStart) purely from resource
    // contention during the bulk sort, not real corruption - misfiling them
    // as personal footage would have been worse than a wrong file name match
    // here, so this runs before the personal-video checks below.
    private static readonly System.Text.RegularExpressions.Regex EpisodePattern =
        new(@"s\d{1,2}e\d{1,2}", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly string[] RipSourceKeywords =
        ["hdtv", "xvid", "dvdrip", "bluray", "blu-ray", "webrip", "web-dl", "webdl", "x264", "x265", "brrip", "camrip", "bdrip"];

    // Nothing previously set video SubCategory at all in the live pipeline -
    // every video silently stayed UnknownVideo (its constructor default)
    // regardless of whether it was a real screen recording or a phone video.
    private static void ClassifyVideoSubCategory(MediaFile mediaFile)
    {
        if (mediaFile.Type != MediaType.Video) return;

        var nameLower = Path.GetFileNameWithoutExtension(mediaFile.FileName).ToLowerInvariant();

        if (EpisodePattern.IsMatch(nameLower) || RipSourceKeywords.Any(k => nameLower.Contains(k)))
        {
            mediaFile.SubCategory = MediaSubCategory.Movie;
            return;
        }

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

    // Checked here, as early as possible - before Hash/Metadata/export/
    // conversion/thumbnails ever touch this file - confirmed 2026-09-06 that
    // a corrupt video was previously only caught by LibraryPolishService's
    // late, post-export QuarantineUnplayableVideos pass, after already
    // paying for every one of those expensive steps on a file that gets
    // routed to Review anyway. Corrupt files should be filtered out fast.
    if (mediaFile.Type == MediaType.Video)
    {
        var duration = _videoMetadataService.GetDuration(mediaFile.FullPath);
        mediaFile.Duration = duration;

        var hasDuration = duration is not null && duration.Value > TimeSpan.Zero;
        if (!hasDuration || !_videoMetadataService.CanDecodeStart(mediaFile.FullPath))
        {
            mediaFile.ExportSubFolder = "Unplayable";
            mediaFile.IsNormalized = false;
            mediaFile.RequiresNormalization = false;
            mediaFile.RequiresEnhancement = false;

            _logger.LogInformation(
                "{File} could not be read/decoded - routed to Review/Unplayable, skipping the rest of the pipeline",
                mediaFile.FileName);

            return;
        }
    }

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
        // Confirmed 2026-09-03: .3gp/.3g2 were only just added to
        // MediaTypeHelper's VideoExtensions (previously landed in
        // Ikke_identificeret) - recognized as video now, but still missing
        // from here meant they fell to the default case and never got
        // flagged for conversion at all.
        case ".3gp":
        case ".3g2":

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