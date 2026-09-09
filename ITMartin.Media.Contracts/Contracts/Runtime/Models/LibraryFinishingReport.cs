namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

// Everything LibraryFinishingService did in one run, in the order it ran -
// this is what LibraryFinishingReportFormatter turns into the Markdown file
// a non-technical person reads the morning after an overnight run. Every
// phase is nullable/empty-default rather than required, so a run that fails
// partway through still produces a report for whatever completed.
public sealed class LibraryFinishingReport
{
    public required string LibraryPath { get; init; }
    public DateTime StartedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime? FinishedAtUtc { get; set; }

    // Phase 0 - rotations a person made in SmartFolders/RoterManuelt, applied
    // back onto the library before anything else reads pixels
    public ManualRotationResult? ManualRotations { get; set; }

    // Phase 1 - reorganization (rotation, junk reclassification, small albums)
    public FreeOrientationFixResult? OrientationFix { get; set; }
    public BurstFlattenResult? BurstsFlattened { get; set; }
    public AlbumArtReclassifyResult? AlbumArtReclassified { get; set; }
    public WebWatermarkReclassifyResult? WebWatermarksReclassified { get; set; }
    public AlbumPruneResult? SmallAlbumsPruned { get; set; }

    // Phase 2 - index converge
    public FileStatusReport? IndexReport { get; set; }
    public int IndexConvergeIterations { get; set; }

    // Phase 3 - dedup per real category folder
    public List<string> CategoryFoldersDeduped { get; init; } = [];
    public int DuplicatesMoved { get; set; }
    public int FilesCheckedForDuplicates { get; set; }

    // Phase 4 - SmartFolders add-ons
    public List<TripFolderResult> Trips { get; init; } = [];
    public List<TraditionResult> Traditions { get; init; } = [];
    public List<YearbookResult> Yearbooks { get; init; } = [];
    public List<PersonFolderResult> UnknownPersonFolders { get; init; } = [];
    public List<SimilarSceneResult> SimilarSceneFolders { get; init; } = [];
    public bool GalleryCollectionsSynced { get; set; }

    // Phase 5 - gallery export
    public StaticGalleryExportResult? GalleryExport { get; set; }

    // Phase 6 - delivery polish (empty folders, OS junk, manifest hidden)
    public LibraryPolishResult? Polish { get; set; }

    // Phase 7 - delivery verification
    public LibraryIntegrityReport? IntegrityReport { get; set; }
    public LibraryStructureReport? StructureReport { get; set; }
    public StructureRepairResult? CollectionsRepair { get; set; }
    public DeliveryStructureReport? DeliveryStructureReport { get; set; }

    // Anything a phase threw that didn't stop the rest of the run - each
    // entry is "{PhaseName}: {ExceptionMessage}", surfaced in the report so
    // a partial failure is visible instead of silently missing from it.
    public List<string> PhaseErrors { get; init; } = [];
}
