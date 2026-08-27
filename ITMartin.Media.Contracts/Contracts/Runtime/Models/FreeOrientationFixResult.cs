namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class FreeOrientationFixResult
{
    public int PhotosChecked { get; init; }
    public int PhotosRotated { get; init; }
    public List<string> NeedsManualReview { get; init; } = [];
}

public sealed class RotatedImageInfo
{
    public string RelativePath { get; init; } = string.Empty;
    public int DegreesNeeded { get; init; }
}

// Report-only counterpart to FreeOrientationFixResult - detects which images
// need rotation via the same free face-detection check but never writes
// anything, for "just show me what's rotated" review before committing to a
// bulk fix.
public sealed class RotationDetectionResult
{
    public int PhotosChecked { get; init; }
    public List<RotatedImageInfo> RotatedImages { get; init; } = [];
    public List<string> NeedsManualReview { get; init; } = [];
}

public sealed class NearDuplicateGroupInfo
{
    public string Kind { get; init; } = string.Empty; // "exact" or "near"
    public List<string> RelativePaths { get; init; } = [];
    public long TotalSizeBytes { get; init; }
}

public sealed class NearDuplicateReport
{
    public int FilesScanned { get; init; }
    public int ExactGroups { get; init; }
    public int NearGroups { get; init; }
    public List<NearDuplicateGroupInfo> Groups { get; init; } = [];
}

// Result of BakeExifOrientationAsync - a free, deterministic fix distinct
// from FixOrientationFreeOnlyAsync/DetectRotatedImagesAsync's face-detection
// guessing. Only touches files that still carry a non-1 EXIF Orientation tag
// (a real, known-correct answer, not an inference) and physically applies it
// to the pixels, so viewers/tools that ignore EXIF (this suite's own
// pipeline included) see the same correct orientation as EXIF-aware ones.
public sealed class BakeOrientationResult
{
    public int PhotosChecked { get; init; }
    public List<string> PhotosFixed { get; init; } = [];
    public List<string> Failed { get; init; } = [];
}
