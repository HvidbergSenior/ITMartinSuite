namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

// One flag = one real pipeline step's verdict on this file. A step that
// doesn't apply to this file's type (e.g. RotationIsCorrect on a video)
// simply never appears here - see FileStatusRecord.ApplicableFlags.
public class FlagState
{
    public bool Value { get; set; }

    // Filled in only when Value is false and a step actually looked at the
    // file and couldn't resolve it - a human-readable reason, not an
    // exception message. Null while a flag has simply never been checked yet.
    public string? Suggestion { get; set; }
}

// The fixed, known vocabulary of step-flags - a file's ApplicableFlags is
// always a subset of these, chosen by its media type. Adding a new pipeline
// capability means adding one name here, not redesigning the record.
public static class StepFlags
{
    public const string CategoryIsSet = "CategoryIsSet";
    public const string SubCategoryIsSet = "SubCategoryIsSet";
    public const string DateIsSet = "DateIsSet";
    public const string RotationIsCorrect = "RotationIsCorrect";
    public const string NotDuplicate = "NotDuplicate";
    public const string IsNormalized = "IsNormalized";
    public const string QualityChecked = "QualityChecked";
    public const string ThumbnailGenerated = "ThumbnailGenerated";
    public const string FileIsReadable = "FileIsReadable";
    public const string FaceIndexed = "FaceIndexed";
    public const string LivePhotoChecked = "LivePhotoChecked";
}

// Persisted per-file record, keyed by content hash, in a library's
// filestatus.json - see IFileStatusRegistryService. Written by whichever
// step resolved something (QuickSort's FileStatusWorkflowStep on a fresh
// import, or LibraryPolishService.RunAllStepsAsync against an already-sorted
// library) and read by every step before doing its own work, so a file that
// reaches IsDone is never touched again by anything, on any future run,
// unless explicitly asked for.
public class FileStatusRecord
{
    public string ContentHash { get; set; } = "";

    // Last known location - for reporting and the cheap path-based fast
    // skip, never the lookup key itself (that's ContentHash).
    public string RelativePath { get; set; } = "";

    // Resolved category folder name (e.g. "Billeder", "Chat", "Memes") -
    // empty until CategoryIsSet/SubCategoryIsSet actually resolve it.
    public string Category { get; set; } = "";

    // Which flags actually apply to this file (decided once, from its media
    // type, when the record is first created) - IsDone only ever checks
    // these, never the full StepFlags vocabulary.
    public List<string> ApplicableFlags { get; set; } = [];

    public Dictionary<string, FlagState> Flags { get; set; } = [];

    // Fast-path skip signal (no hashing needed) - see FileStatusRegistryService.
    public long SizeBytes { get; set; }
    public DateTimeOffset LastWriteUtc { get; set; }

    public DateTimeOffset LastUpdatedUtc { get; set; }

    // True the moment every flag that applies to this file is true. Once
    // true, no step should touch this file again on a future run unless the
    // caller explicitly forces a re-check.
    public bool IsDone =>
        ApplicableFlags.Count > 0 &&
        ApplicableFlags.All(f => Flags.TryGetValue(f, out var s) && s.Value);
}

public class FileStatusReport
{
    public int TotalFiles { get; set; }
    public int DoneFiles { get; set; }
    public Dictionary<string, int> ByCategory { get; set; } = [];

    // Per-flag: how many applicable files still have it false, one bucket
    // per flag name - the "what's actually left to do" view.
    public Dictionary<string, int> OutstandingByFlag { get; set; } = [];

    // A few real (file, flag, suggestion) triples per outstanding flag, so
    // the report is actionable, not just a count.
    public List<OutstandingItem> Sample { get; set; } = [];
}

public class OutstandingItem
{
    public string RelativePath { get; set; } = "";
    public string Flag { get; set; } = "";
    public string? Suggestion { get; set; }
}
