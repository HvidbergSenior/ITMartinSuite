using ITMartinFileSorter.Domain.Enums;

public class MediaFile
{
    public string FullPath { get; }
    public string OriginalPath { get; }
    public string FileName { get; }
    public string Extension { get; }

    public long SizeBytes { get; set; }

    public DateTime? CreatedAt { get; private set; }
    public int Year { get; private set; }
    public int Month { get; private set; }

    public MediaType Type { get; }

    public MediaMainCategory MainCategory =>
        Type switch
        {
            MediaType.Audio => MediaMainCategory.Audio,
            MediaType.Video => MediaMainCategory.Video,
            MediaType.Document => MediaMainCategory.Document,
            MediaType.Image => MediaMainCategory.Image,
            _ => MediaMainCategory.Image
        };

    public MediaSubCategory SubCategory { get; set; }
    public MediaTertiaryCategory TertiaryCategory { get; set; } = MediaTertiaryCategory.Unknown;

    public MediaSource Source { get; set; } = MediaSource.Unknown;

    public string? Hash { get; private set; }
    public List<string> Tags { get; } = new();

    public int? Width { get; set; }
    public int? Height { get; set; }

    public bool IsDateReliable { get; private set; }

    public bool IsImage => Type == MediaType.Image;
    public bool IsVideo => Type == MediaType.Video;
    public MediaFileStatus Status { get; set; } = MediaFileStatus.Initial;
    public bool RequiresReview { get; set; } = true;
    public bool IsProbablyRealPhoto { get; set; }
    public bool HasExif { get; set; }
    public MediaFile(string fullPath, DateTime? createdAt, MediaType type, long sizeBytes)
    {
        FullPath = fullPath;
        OriginalPath = fullPath;

        FileName = Path.GetFileName(fullPath);
        Extension = Path.GetExtension(fullPath);

        SizeBytes = sizeBytes;
        Type = type;

        // ✅ default subcategory based on type
        SubCategory = type switch
        {
            MediaType.Audio => MediaSubCategory.UnknownAudio,
            MediaType.Video => MediaSubCategory.UnknownVideo,
            MediaType.Document => MediaSubCategory.UnknownDocument,
            MediaType.Image => MediaSubCategory.UnknownImage,
            _ => MediaSubCategory.UnknownImage
        };

        if (createdAt != null)
            SetDate(createdAt.Value, true);
    }

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
}