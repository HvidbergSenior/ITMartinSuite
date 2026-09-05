using ITMartin.Media.Contracts.Contracts.Runtime.Enums;

namespace ITMartin.Media.Infrastructure.Media;

public static class MediaTypeHelper
{
    private static readonly HashSet<string>
        VideoExtensions =
        [
            ".mp4",
            ".mkv",
            ".avi",
            ".mov",
            ".mpg",
            ".mpeg",
            ".mts",
            ".m2ts",
            ".wmv",
            ".vob",
            ".m4v",
            // Found 2026-09-03 on Rico/AC's whole-drive backup archive: real
            // mobile-phone videos in the older 3GPP container landed in
            // Ikke_identificeret instead of Videoer, same failure shape as
            // the .wma gap found on mie's library below.
            ".3gp",
            ".3g2"
        ];

    private static readonly HashSet<string>
        ImageExtensions =
        [
            ".jpg",
            ".jpeg",
            ".png",
            ".webp",
            ".bmp",
            ".gif",
            ".tif",
            ".tiff",
            ".heic",
            ".heif",
            ".avif"
        ];

    private static readonly HashSet<string>
        AudioExtensions =
        [
            ".mp3",
            ".wav",
            ".flac",
            ".aac",
            ".ogg",
            ".m4a",
            // Found 2026-08-25 on mie's real library: these three landed in
            // Ikke_identificeret (the unrecognized-type bucket) instead of
            // Musik - .wma alone was 182 real files, easy to mistake for
            // junk sitting next to genuine iTunes-library cache clutter.
            ".wma",
            ".opus",
            ".m4r",
            ".m4b",
            // Found 2026-09-03 alongside the .3gp/.tif gaps on the same
            // archive - real MIDI music files landed in Ikke_identificeret.
            ".mid",
            ".midi"
        ];

    private static readonly HashSet<string>
        DocumentExtensions =
        [
            ".pdf",
            ".doc",
            ".docx",
            ".xls",
            ".xlsx",
            ".ppt",
            ".pptx",
            ".odt",
            ".rtf",
            ".oxps",
            ".txt",
            ".ifo",
            ".bup"
        ];

    public static bool IsVideo(
        string path)
    {
        var extension =
            GetExtension(path);

        return VideoExtensions
            .Contains(extension);
    }

    public static bool IsImage(
        string path)
    {
        var extension =
            GetExtension(path);

        return ImageExtensions
            .Contains(extension);
    }

    public static bool IsAudio(
        string path)
    {
        var extension =
            GetExtension(path);

        return AudioExtensions
            .Contains(extension);
    }

    public static bool IsDocument(
        string path)
    {
        var extension =
            GetExtension(path);

        return DocumentExtensions
            .Contains(extension);
    }

    public static MediaType GetMediaType(
        string path)
    {
        if (IsVideo(path))
        {
            return MediaType.Video;
        }

        if (IsImage(path))
        {
            return MediaType.Image;
        }

        if (IsAudio(path))
        {
            return MediaType.Audio;
        }

        if (IsDocument(path))
        {
            return MediaType.Document;
        }

        // Genuinely unrecognized (DB table files, app config/cache junk, etc.)
        // - must NOT silently fall back to Image, or technical clutter from a
        // raw source folder ends up masquerading as photos in the export.
        return MediaType.Unknown;
    }

    private static string GetExtension(
        string path)
    {
        return Path.GetExtension(path)
            .ToLowerInvariant();
    }
}