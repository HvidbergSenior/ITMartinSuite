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

    // Standalone counterpart to FileStatusWorkflowStep, for a library that's
    // already sorted (not a fresh Package1 import) - e.g. re-running against
    // D:\mie after the fact. Shares the same filestatus.json registry, so a
    // file resolved by either path is recognized by the other. Runs every
    // applicable step-flag (CategoryIsSet, SubCategoryIsSet, DateIsSet,
    // RotationIsCorrect (free tier), NotDuplicate, IsNormalized,
    // QualityChecked, FileIsReadable) against whatever isn't already IsDone -
    // a file with every applicable flag true is skipped entirely on the next
    // call, so repeated runs against the same library only ever get cheaper.
    // Images still ambiguous after the free tiers fall to a capped number of
    // real Claude calls (is_screenshot/is_meme/is_chat, same call also
    // answers QualityChecked at no extra cost) - maxAiCalls is a required
    // hard cap, same convention as every other per-file AI pass in this suite.
    //
    // includeSlowSteps=false skips the two genuinely slow phases - rotation
    // face-detection and AI classification - entirely (no ONNX/Claude calls
    // made at all). Structure/format flags (CategoryIsSet, SubCategoryIsSet
    // when resolvable from EXIF alone, NotDuplicate, IsNormalized, DateIsSet,
    // FileIsReadable) still get resolved and saved, so a library lands in its
    // correct folders and gets its conversion needs flagged as fast as
    // possible. Files left with an unresolved RotationIsCorrect/ambiguous
    // SubCategoryIsSet simply aren't IsDone yet - a later includeSlowSteps=true
    // call picks them back up without re-deriving anything already resolved.
    // maxRotationChecksPerRun bounds how many photos get the expensive free
    // rotation check (4x decode+ONNX each) in this ONE call - the actual fix
    // for a call that used to take 7-12+ hours against a large backlog.
    // Defaults to 500 (same value FixOrientationAsync already uses). Anything
    // past the cap is left unresolved, not quarantined - a future call (see
    // RunUntilConvergedAsync) picks it up, since the front of the queue is
    // guaranteed to shrink every round (each checked photo is either fixed
    // in place or moved to RotationUkendt, never left pending).
    // maxFilesScannedPerRun bounds the sequential scan phase (hash + EXIF +
    // video metadata, one ffprobe spawn per video) the same way
    // maxRotationChecksPerRun bounds the rotation phase - found necessary
    // 2026-08-24 running against a video-heavy backlog, where the scan phase
    // alone could dominate a call's wall-clock time before rotation-checking
    // was ever reached. Defaults to 3000. Same shrinks-every-round
    // convergence via RunUntilConvergedAsync.
    Task<FileStatusReport> RunAllStepsAsync(string libraryPath, int maxAiCalls, int? maxRotationParallelism = null, bool includeSlowSteps = true, int? maxRotationChecksPerRun = null, int? maxFilesScannedPerRun = null, CancellationToken cancellationToken = default);

    // Automates "keep calling RunAllStepsAsync until it stops making
    // progress" - each round only touches files that aren't IsDone yet, and
    // unresolvable rotation cases get quarantined into RotationUkendt, so the
    // residual naturally shrinks (and gets cheaper) every round without
    // re-deriving anything already known. Stops early once every file is
    // IsDone, or once a round makes zero additional progress over the last
    // one (further calls would just re-pay the same cheap walk for the same
    // irreducible residual) - maxIterations is a hard safety ceiling either
    // way, same "always a real cap" convention as every other loop in this
    // suite.
    Task<FileStatusReport> RunUntilConvergedAsync(string libraryPath, int maxAiCallsPerIteration, int? maxRotationParallelism = null, int maxIterations = 10, CancellationToken cancellationToken = default);

    // One-time cleanup for a specific mess found in already-sorted libraries:
    // "BurstN" subfolders (not produced by any current pipeline step) that
    // group a handful of files under a Year/Month folder. Moves every file
    // back up into the parent folder (collision-safe rename) and removes the
    // now-empty BurstN folder. Never touches a folder that isn't literally
    // named "Burst" followed by digits.
    Task<BurstFlattenResult> FlattenBurstFoldersAsync(string libraryPath, CancellationToken cancellationToken = default);

    // Post-hoc counterpart to MediaRulesWorkflowStep's Package1-time album-art
    // detection: catches art that was already sitting in Billeder BEFORE that
    // fix existed (an already-sorted library, not a fresh import), where the
    // original audio-sibling-in-same-folder signal no longer applies since
    // Package1 already split images and audio into separate category trees.
    // Only the unambiguous "AlbumArt <GUID> Large" cache-file pattern (Windows
    // Media Player/Zune's own naming, never a real photo) is moved
    // automatically; everything else that merely matches a cover/front/back-
    // style filename is reported in ReviewCandidates, not moved, since that
    // heuristic alone has a known false-positive (a Facebook timeline cover
    // photo, found on mie's real library 2026-08-25). Free, local, no AI.
    Task<AlbumArtReclassifyResult> ReclassifyAlbumArtAsync(string libraryPath, CancellationToken cancellationToken = default);

    // Generalizes ReclassifyAlbumArtAsync's filename-only matching - found
    // necessary 2026-08-25 when a whole folder of band promo/single-cover
    // scans (Billeder/2000/Ukendt måned - "fTAP.jpg", "greenus.jpg",
    // "triplike.jpg", none matching any cover/front/back filename hint) sat
    // right next to files that DID match. The tell wasn't the name, it was
    // the folder: every file in it was small (web-scraped/scanned art, not
    // camera output) and had no camera EXIF at all. Groups files by their
    // containing leaf folder and flags any folder where most files are both
    // small/low-resolution and EXIF-camera-less (dimensions checked, not just
    // file size, so a heavily-compressed but full-resolution real photo isn't
    // caught by size alone; .gif counts on its own - never real camera
    // output). Messaging-app filenames (image0 f4b1ccbc.jpeg, IMG_0001.jpg,
    // WhatsApp Image...) are excluded outright - those apps strip EXIF and
    // re-compress on save, which otherwise looks identical to downloaded art
    // (found on mie's real library 2026-08-25: Billeder/2021/Ukendt måned,
    // 38 files, all real photos). Report-only, never moves anything, since
    // folder-level heuristics are inherently softer than a single confirmed
    // filename pattern.
    Task<NonPhotoClusterReport> FindNonPhotoClustersAsync(string libraryPath, CancellationToken cancellationToken = default);
}
