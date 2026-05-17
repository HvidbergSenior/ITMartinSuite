using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Enums;

namespace ITMartin.Media.Application.Processors;

public class FileCreationProcessor
{
    public MediaFile Create(
        string fullPath,
        long sizeBytes,
        DateTime lastWrite)
    {
        var extension =
            Path.GetExtension(fullPath);

        var type =
            FileScannerTypes
                .IsImage(extension)
                    ? MediaType.Image
                    : FileScannerTypes
                        .IsVideo(extension)
                        ? MediaType.Video
                        : FileScannerTypes
                            .IsAudio(extension)
                            ? MediaType.Audio
                            : MediaType.Document;

        var file =
            new MediaFile(
                fullPath:
                    fullPath,

                createdAt:
                    lastWrite,

                type:
                    type,

                sizeBytes:
                    sizeBytes);

        // ====================================
        // PACKAGE 1 DEFAULTS
        // ====================================

        file.Status =
            MediaFileStatus.ToKeep;

        file.RequiresReview =
            false;

        return file;
    }
}

public static class FileScannerTypes
{
    public static readonly HashSet<string>
        ImageExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png",
            ".gif", ".bmp", ".webp",
            ".tiff", ".tif",
            ".heic", ".heif",
            ".avif"
        };

    public static readonly HashSet<string>
        VideoExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4",
            ".mov",
            ".avi",
            ".mkv",
            ".wmv"
        };

    public static readonly HashSet<string>
        AudioExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3",
            ".wav",
            ".flac",
            ".aac",
            ".ogg"
        };

    public static bool IsImage(
        string extension)
    {
        return ImageExtensions
            .Contains(extension);
    }

    public static bool IsVideo(
        string extension)
    {
        return VideoExtensions
            .Contains(extension);
    }

    public static bool IsAudio(
        string extension)
    {
        return AudioExtensions
            .Contains(extension);
    }
}