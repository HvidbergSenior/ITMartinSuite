    using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
    using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
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

        // True when the source file carried a usable EXIF Orientation tag
        // (any value 1-8) at import time - Package1 already baked it into the
        // pixels correctly (see ImageConverterService), so FileStatusWorkflowStep
        // can trust RotationIsCorrect immediately instead of deferring to the
        // expensive face-detection fallback. False means no tag existed at
        // all - genuinely unknown, the only case worth the expensive check.
        public bool OrientationKnownFromExif { get; set; }

        // Null = never examined this run. Set by the free local quality
        // check (every image, no AI cost) and overwritten by the paid AI
        // classification's own verdict when that's also enabled, since the
        // AI result is the more reliable of the two.
        public bool? IsBlurry { get; set; }

        public bool? IsSolidColor { get; set; }

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
                    MediaMainCategory.Other
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

        // True when only Year came from a trustworthy source (an ancestor
        // folder name) - Month/Day are placeholders, never a real date.
        // Export routing uses this to land the file in "{year}/Ukendt måned"
        // instead of a fully-dated month folder or the flat Undated bucket.
        public bool IsYearOnly { get; private set; }

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
        public WorkflowType? WorkflowType { get; set; }
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
                // Audio is just copied, never run through classification -
                // there's no pipeline step that ever resolves UnknownAudio to
                // anything else, so defaulting to it here would leave every
                // audio file's SubCategoryIsSet flag false forever (see
                // FileStatusWorkflowStep) and it would never reach IsDone.
                MediaType.Audio =>
                    MediaSubCategory.Music,

                MediaType.Video =>
                    MediaSubCategory.UnknownVideo,

                MediaType.Document =>
                    MediaSubCategory.UnknownDocument,

                MediaType.Image =>
                    MediaSubCategory.UnknownImage,

                _ =>
                    MediaSubCategory.UnknownOther
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
            bool isReliable,
            bool isYearOnly = false)
        {
            CreatedAt = date;

            IsDateReliable =
                isReliable;

            IsYearOnly =
                isYearOnly;

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