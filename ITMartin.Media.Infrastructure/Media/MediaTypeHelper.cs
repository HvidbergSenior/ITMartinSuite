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
            ".vob"
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
            ".tiff"
        ];

    private static readonly HashSet<string>
        AudioExtensions =
        [
            ".mp3",
            ".wav",
            ".flac",
            ".aac",
            ".ogg",
            ".m4a"
        ];

    private static readonly HashSet<string>
        DocumentExtensions =
        [
            ".pdf",
            ".doc",
            ".docx",
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

        return MediaType.Image;
    }

    private static string GetExtension(
        string path)
    {
        return Path.GetExtension(path)
            .ToLowerInvariant();
    }
}