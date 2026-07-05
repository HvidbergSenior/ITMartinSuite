namespace ITMartin.Media.Contracts.Contracts.Runtime.Helpers;

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
            ".wmv"
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
            ".aac",
            ".flac",
            ".ogg",
            ".m4a"
        ];

    public static bool IsVideo(
        string path)
    {
        var extension =
            Path.GetExtension(path)
                .ToLowerInvariant();

        return VideoExtensions
            .Contains(extension);
    }

    public static bool IsImage(
        string path)
    {
        var extension =
            Path.GetExtension(path)
                .ToLowerInvariant();

        return ImageExtensions
            .Contains(extension);
    }

    public static bool IsAudio(
        string path)
    {
        var extension =
            Path.GetExtension(path)
                .ToLowerInvariant();

        return AudioExtensions
            .Contains(extension);
    }
}