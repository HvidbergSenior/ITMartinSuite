using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

// Final delivery-readiness pass over an already-organized library - removes
// clutter a non-technical recipient shouldn't have to see (empty category
// folders, OS-generated junk files) and hides internal bookkeeping files
// rather than deleting them, since other pipeline steps still read them.
public interface ILibraryPolishService
{
    Task<LibraryPolishResult> PolishAsync(string libraryPath, CancellationToken cancellationToken = default);

    // Content-based rotation fix, separate from the free PolishAsync pass since
    // it makes real Claude API calls - opt-in only, never run automatically.
    // Needed because some already-delivered libraries have photos baked in
    // sideways/upside-down with no original source file left to re-read a
    // correct EXIF Orientation tag from (see ImageConverterService's
    // ApplyOriginalOrientation, which only helps on *future* conversions).
    Task<OrientationFixResult> FixOrientationAsync(string libraryPath, CancellationToken cancellationToken = default);

    // One-off pass over Udaterede/{Images,Videos} using the current (correct)
    // date-resolution logic - catches files that landed here from an older
    // Package1 run whose metadata reading has since been fixed, or files
    // whose EXIF simply wasn't read correctly the first time. Free (no AI
    // calls) - safe to run as often as needed.
    Task<RedateUndatedResult> RedateUndatedAsync(string libraryPath, CancellationToken cancellationToken = default);

    // One-off re-grouping pass: pulls every photo under Billeder whose EXIF
    // Make/Model contains the given text out into its own top-level folder
    // (e.g. all Olympus shots into "Olympus Camera"). Free (EXIF read only,
    // no AI) - safe to run as often as needed.
    Task<CameraGroupResult> GroupByCameraMakeAsync(string libraryPath, string makeContains, string targetFolderName, CancellationToken cancellationToken = default);

    // Byte-identical duplicate removal, scoped to exactly one folder's own
    // subtree - never compares across folder boundaries, since a SmartFolders
    // copy being byte-identical to its Billeder original is by design, not a
    // bug (see SmartFoldersService - real copies, not symlinks). Keeps the
    // first file (ordinal filename order) in each duplicate group, deletes
    // the rest. Real, irreversible deletion - caller's responsibility to
    // have confirmed with the user first.
    Task<DeduplicateResult> DeduplicateFolderAsync(string folderPath, CancellationToken cancellationToken = default);
}
