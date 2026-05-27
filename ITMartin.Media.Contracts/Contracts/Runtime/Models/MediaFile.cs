    using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
    using ITMartin.Media.Contracts.Entities;

    namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

    public class MediaFile
    {
        public Guid Id { get; set; } =
            Guid.NewGuid();

        public string FullPath { get; }

        public string OriginalPath { get; }

        public string FileName { get; }

        public string Extension { get; }

        public long SizeBytes { get; set; }

        public DateTime? CreatedAt { get; private set; }

        public int Year { get; private set; }

        public int Month { get; private set; }

        public string? AiCategory { get; set; }

        public string? AiSubCategory { get; set; }

        public string? AiDescription { get; set; }

        public float? AiConfidence { get; set; }

        public bool AiProcessed { get; set; }

        public MediaType Type { get; }
        public bool RequiresNormalization { get; set; }

        public bool RequiresEnhancement { get; set; }

        public bool IsNormalized { get; set; }

        public bool IsEnhanced { get; set; }
        public CleanupDecision CleanupDecision { get; set; } =
            CleanupDecision.Keep;
        public string? ExportSubFolder { get; set; }
        public List<MediaSegment> Segments { get; set; } = [];
        
        public MediaMainCategory MainCategory =>
            Type switch
            {
                MediaType.Audio =>
                    MediaMainCategory.Audio,

                MediaType.Video =>
                    MediaMainCategory.Video,

                MediaType.Document =>
                    MediaMainCategory.Document,

                MediaType.Image =>
                    MediaMainCategory.Image,

                _ =>
                    MediaMainCategory.Image
            };

        public MediaSubCategory
            SubCategory { get; set; }

        public MediaTertiaryCategory
            TertiaryCategory { get; set; } =
                MediaTertiaryCategory.Unknown;

        public MediaSource
            Source { get; set; } =
                MediaSource.Unknown;

        public string? Hash { get; private set; }

        public List<string> Tags { get; } = [];

        public int? Width { get; set; }

        public int? Height { get; set; }

        public TimeSpan? Duration { get; set; }

        public bool IsDateReliable { get; private set; }

        public bool IsImage =>
            Type == MediaType.Image;

        public bool IsVideo =>
            Type == MediaType.Video;

        public bool IsAudio =>
            Type == MediaType.Audio;

        public bool IsDocument =>
            Type == MediaType.Document;

        public MediaFileStatus
            Status { get; set; } =
                MediaFileStatus.Initial;

        public bool RequiresReview { get; set; } = true;

        public bool IsProbablyRealPhoto { get; set; }

        public bool HasExif { get; set; }

        public string? ExportedPath { get; set; }

        public string? NormalizedPath { get; set; }

        public string? ThumbnailPath { get; set; }

        public string? OcrText { get; set; }

        public bool OcrProcessed { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        public string? CameraModel { get; set; }

        public string? Artist { get; set; }

        public string? Album { get; set; }

        public string? Title { get; set; }

        public int? TrackNumber { get; set; }

        public int? PageCount { get; set; }

        public string? Author { get; set; }

        public string? DocumentTitle { get; set; }
        public bool Failed { get; set; }

        public List<string> AiTags { get; set; } = [];

        public MediaFile(
            string fullPath,
            DateTime? createdAt,
            MediaType type,
            long sizeBytes,
            bool isDateReliable = false)
        {
            FullPath = fullPath;

            OriginalPath = fullPath;

            FileName =
                Path.GetFileName(fullPath);

            Extension =
                Path.GetExtension(fullPath);

            SizeBytes =
                sizeBytes;

            Type = type;

            SubCategory = type switch
            {
                MediaType.Audio =>
                    MediaSubCategory.UnknownAudio,

                MediaType.Video =>
                    MediaSubCategory.UnknownVideo,

                MediaType.Document =>
                    MediaSubCategory.UnknownDocument,

                MediaType.Image =>
                    MediaSubCategory.UnknownImage,

                _ =>
                    MediaSubCategory.UnknownImage
            };

            if (createdAt != null)
            {
                SetDate(
                    createdAt.Value,
                    isDateReliable);
            }
        }

        public void SetDate(
            DateTime? date,
            bool isReliable)
        {
            CreatedAt = date;

            IsDateReliable =
                isReliable;

            if (date is { } d)
            {
                Year = d.Year;
                Month = d.Month;
            }
        }

        public void SetHash(
            string hash)
        {
            Hash = hash;
        }
    }