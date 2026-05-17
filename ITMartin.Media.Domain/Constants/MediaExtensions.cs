namespace ITMartin.Media.Domain.Constants;

public static class MediaExtensions
{
    public static readonly HashSet<string> ImageExtensions =
    [
        ".jpg",
        ".jpeg",
        ".png",
        ".gif",
        ".bmp",
        ".webp",
        ".heic",
        ".tiff"
    ];

    public static readonly HashSet<string> VideoExtensions =
    [
        ".mp4",
        ".mov",
        ".avi",
        ".mkv",
        ".wmv",
        ".flv",
        ".webm"
    ];

    public static readonly HashSet<string> DocumentExtensions =
    [
        ".pdf",
        ".doc",
        ".docx",
        ".txt",
        ".rtf"
    ];

    public static readonly HashSet<string> AudioExtensions =
    [
        ".mp3",
        ".wav",
        ".aac",
        ".flac",
        ".ogg"
    ];
}