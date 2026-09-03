namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public enum FaceIndexIndexType
{
    Faces
}

public sealed class PersonDto
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public int ReferencePhotoCount { get; init; }
}

public sealed class PersonMatchResult
{
    public required string MediaFilePath { get; init; }
    public double Confidence { get; init; }
    public bool UserConfirmed { get; init; }
}

public sealed class FaceIndexIndexStatus
{
    public required string Status { get; init; } // Running | Completed | Failed
    public int TotalFiles { get; init; }
    public int ProcessedFiles { get; init; }
    public string? CurrentFile { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed record ReferencePhotoInput(string FileName, byte[] Bytes);

/// <summary>
/// One discovered but not-yet-named face cluster - the "who's actually in this
/// library" discovery step. SampleMediaFilePath is just the first photo in the
/// cluster, shown so a human can recognize the face before naming it.
/// </summary>
public sealed class UnnamedPersonCluster
{
    public required string SampleMediaFilePath { get; init; }
    public required IReadOnlyList<string> MediaFilePaths { get; init; }
    public int PhotoCount => MediaFilePaths.Count;
}

/// <summary>
/// Result of trying to date Undated files by matching them (via face or GPS
/// proximity) against already-dated content elsewhere in the library.
/// </summary>
public sealed class UndatedEstimationResult
{
    public int MovedByFaceMatch { get; init; }
    public int MovedByGpsMatch { get; init; }
    public int StillUndated { get; init; }
}

/// <summary>One AI-classified verdict for a single Unhandled file.</summary>
public sealed class UnhandledClassificationItem
{
    public Guid Id { get; init; }

    /// <summary>"Images" | "Videos" | "Documents" | "Audio" | "DeleteCandidate" | "KeepUnhandled".</summary>
    public required string Verdict { get; init; }

    public double Confidence { get; init; }
}

/// <summary>
/// Result of running AI classification over the Unhandled folder - text-only
/// (filenames/paths), no image bytes, so this is cheap even at scale.
/// </summary>
public sealed class UnhandledClassificationResult
{
    public int Reclassified { get; init; }
    public int MarkedForDeletion { get; init; }
    public int StillUnhandled { get; init; }
    public int SkippedOverCap { get; init; }
}
