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

    // Re-checks every file already sorted into the Screenshots/Skærmbilleder
    // category against Claude's own is_screenshot judgment (real app/phone UI
    // chrome - status bar, buttons, nav icons - visible in the image), not
    // just the extension/folder it landed in during Package1. Anything NOT a
    // real screenshot moves to Images/Billeder's own Andet subfolder instead
    // of staying miscategorized. Opt-in only, same as FixOrientationAsync -
    // makes real (Haiku-cheap but real) Claude API calls per file.
    Task<ScreenshotReclassifyResult> ReclassifyScreenshotsAsync(string libraryPath, int maxFiles = 500, CancellationToken cancellationToken = default);

    // Free-only rotation fix - the same face-detection tier FixOrientationAsync
    // already tries before falling back to a paid Claude call, but exposed on
    // its own so it can run without ever touching that fallback. Auto-fixes
    // whatever it's confident about (finds faces, one consistent rotation);
    // anything ambiguous (no face, or faces at multiple angles) is reported
    // in NeedsManualReview rather than guessed at or silently skipped - never
    // costs anything, never automatic-only, always leaves a manual list.
    Task<FreeOrientationFixResult> FixOrientationFreeOnlyAsync(string libraryPath, CancellationToken cancellationToken = default);

    // Report-only counterpart to FixOrientationFreeOnlyAsync - same free
    // face-detection check, but never calls ApplyResolvedFile, so nothing on
    // disk changes. For "just show me what's rotated" before committing to a
    // bulk fix.
    Task<RotationDetectionResult> DetectRotatedImagesAsync(string libraryPath, CancellationToken cancellationToken = default);

    // Runs against an already-sorted/delivered library (not a fresh Package1
    // import) - IDuplicateService's exact-hash + perceptual-hash passes only
    // ever compare files within one Package1 run, so duplicates introduced
    // by merging separate folders/runs together after the fact (see
    // feedback_hd_delivery_verification) are never caught by it. Free, local,
    // never auto-deletes: exact byte-identical matches are unambiguous but
    // still only reported (same as near-duplicates) - deletion is the
    // caller's call, same convention as DeduplicateFolderAsync's own
    // "caller's responsibility to have confirmed with the user first".
    Task<NearDuplicateReport> FindDuplicatesInLibraryAsync(string libraryPath, CancellationToken cancellationToken = default);

    // Reverse of ReclassifyScreenshotsAsync: scans an Images/Billeder-side
    // folder (top-level only, not recursive - caller picks the exact scope,
    // e.g. just an Udaterede pile rather than the whole category) for real
    // phone/app screenshots that were never routed to Skærmbilleder in the
    // first place, and moves them there. Same real (Haiku-cheap but real)
    // per-file Claude cost as ReclassifyScreenshotsAsync - opt-in only,
    // required maxFiles cap.
    Task<ScreenshotReclassifyResult> FindScreenshotsInImagesAsync(string sourceFolder, string destScreenshotsFolder, int maxFiles, CancellationToken cancellationToken = default);

    // Byte-identical duplicate removal, scoped to exactly one folder's own
    // subtree - never compares across folder boundaries, since a SmartFolders
    // copy being byte-identical to its Billeder original is by design, not a
    // bug (see SmartFoldersService - real copies, not symlinks). Keeps the
    // first file (ordinal filename order) in each duplicate group, deletes
    // the rest. Real, irreversible deletion - caller's responsibility to
    // have confirmed with the user first.
    Task<DeduplicateResult> DeduplicateFolderAsync(string folderPath, CancellationToken cancellationToken = default);
}
