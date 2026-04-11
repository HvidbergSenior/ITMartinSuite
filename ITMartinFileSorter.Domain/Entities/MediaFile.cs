using ITMartinFileSorter.Domain.Enums;

namespace ITMartinFileSorter.Domain.Entities;

public class MediaFile
{
    public string FullPath { get; }
    public string OriginalPath { get; }
    public string FileName { get; }
    public string Extension { get; }
    public long SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public MediaType Type { get; }

    public string? WorkingPath { get; set; }  // points to the temporary copy
    public string? ThumbnailPath { get; set; }

    // Categorization
    public MediaMainCategory MainCategory { get; set; }
    public MediaSubCategory SubCategory { get; set; }
    public MediaTertiaryCategory TertiaryCategory { get; set; }

    // Metadata
    public string? DeviceModel { get; set; }
    public string? Location { get; set; }
    public string? Hash { get; private set; }

    public string? DynamicFolder { get; set; }

    // Video metadata
    public long? DurationMs { get; private set; }
    public int? Width { get; private set; }
    public int? Height { get; private set; }

    public string ExportPath => WorkingPath ?? FullPath;
    
    // Tracks whether user wants to keep the file in the workflow
    public MediaFileStatus Status { get; set; } = MediaFileStatus.Initial;
    public MediaFile(string fullPath, DateTime createdAt, MediaType type, long sizeBytes)
    {
        FullPath = fullPath;
        OriginalPath = fullPath;

        FileName = Path.GetFileName(fullPath);
        Extension = Path.GetExtension(fullPath);

        SizeBytes = sizeBytes;
        CreatedAt = createdAt;

        Year = createdAt.Year;
        Month = createdAt.Month;

        Type = type;

        MainCategory = type switch
        {
            MediaType.Audio => MediaMainCategory.Audio,
            MediaType.Video => MediaMainCategory.Video,
            MediaType.Document => MediaMainCategory.Document,
            MediaType.Image => MediaMainCategory.Image,
            _ => MediaMainCategory.Image
        };

        SubCategory = type switch
        {
            MediaType.Audio => MediaSubCategory.UnknownAudio,
            MediaType.Video => MediaSubCategory.UnknownVideo,
            MediaType.Document => MediaSubCategory.UnknownDocument,
            MediaType.Image => MediaSubCategory.UnknownImage,
            _ => MediaSubCategory.UnknownImage
        };

        TertiaryCategory = MediaTertiaryCategory.Unknown;
    }

    public void SetHash(string hash) => Hash = hash;

    public void SetVideoMetadata(long? durationMs, int? width, int? height)
    {
        DurationMs = durationMs;
        Width = width;
        Height = height;
    }
    public override string ToString()
        => $"FileName={FileName}, FullPath={FullPath}, DynamicFolder={DynamicFolder}, Status={Status}";
}