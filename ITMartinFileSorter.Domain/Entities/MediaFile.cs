using ITMartinFileSorter.Domain.Enums;

namespace ITMartinFileSorter.Domain.Entities;

public class MediaFile
{
    public string FullPath { get; }
    public string OriginalPath { get; }
    public string FileName { get; }
    public string Extension { get; }
    public long SizeBytes { get; set; }

    public DateTime? CreatedAt { get; set; }
    public bool IsDateReliable { get; set; }  // ✅ NEW

    public int Year { get; set; }
    public int Month { get; set; }
    public MediaType Type { get; }

    public string? WorkingPath { get; set; }
    public string? ThumbnailPath { get; set; }

    public MediaMainCategory MainCategory { get; set; }
    public MediaSubCategory SubCategory { get; set; }
    public MediaTertiaryCategory TertiaryCategory { get; set; }

    public string? DeviceModel { get; set; }
    public string? Location { get; set; }
    public string? Hash { get; private set; }
    public string? UserFolderName { get; set; }
    public string? UserTitle { get; set; }
    public List<string> Tags { get; set; } = new();

    public string? DynamicFolder { get; set; }

    public long? DurationMs { get; private set; }
    public int? Width { get; set; }
    public int? Height { get; set; }

    public bool HasExif { get; set; }
    public string? Format { get; set; }

    public string ExportPath => WorkingPath ?? FullPath;

    public MediaFileStatus Status { get; set; } = MediaFileStatus.Initial;

    public MediaFile(string fullPath, DateTime? createdAt, MediaType type, long sizeBytes)
    {
        FullPath = fullPath;
        OriginalPath = fullPath;

        FileName = Path.GetFileName(fullPath);
        Extension = Path.GetExtension(fullPath);

        SizeBytes = sizeBytes;
        Type = type;

        if (createdAt != null)
        {
            SetDate(createdAt.Value, true); // ✅ ALWAYS go through method
        }

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

    // ✅ CENTRALIZED DATE SETTER
    public void SetDate(DateTime? date, bool isReliable)
    {
        CreatedAt = date;
        IsDateReliable = isReliable;

        if (date is { } d)
        {
            Year = d.Year;
            Month = d.Month;
        }
    }

    public void SetHash(string hash) => Hash = hash;

    public void SetVideoMetadata(long? durationMs, int? width, int? height)
    {
        DurationMs = durationMs;
        Width = width;
        Height = height;
    }

    public override string ToString()
        => $"FileName={FileName}, Date={CreatedAt}, Reliable={IsDateReliable}, Status={Status}";
}