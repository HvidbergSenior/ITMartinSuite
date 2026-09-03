using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

// LibraryVerify - library health check. Actually tries to open/decode every file
// in a sorted library rather than trusting extension or codec metadata, so
// it catches files that would silently fail to play/view in the gallery.
public interface ILibraryVerifyService
{
    Task<LibraryIntegrityReport> VerifyLibraryAsync(string libraryPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the library is structured the way the gallery/export code
    /// expects, regardless of which drive/machine libraryPath currently points
    /// at (an external HD or the NAS mount both work - this is metadata-only,
    /// no file content is opened/decoded). Confirms the expected top-level
    /// category folders exist, and that every path stored in collections.json
    /// is relative, forward-slash, and actually resolves under libraryPath -
    /// the exact bug class that once shipped a Windows-only path into a
    /// collection that silently showed zero photos once served from the NAS's
    /// Linux container. Read-only; never modifies anything.
    /// </summary>
    Task<LibraryStructureReport> VerifyStructureAsync(string libraryPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fixes what VerifyStructureAsync can find in collections.json in place -
    /// backslash separators normalized to forward slashes, entries pointing at
    /// files that no longer exist under libraryPath dropped - without needing
    /// to copy the library back locally or re-run QuickSort (which isn't safe
    /// against already-sorted output). Only ever touches collections.json.
    /// </summary>
    Task<StructureRepairResult> RepairCollectionsPathsAsync(string libraryPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Automated "does this delivered HD/USB actually look right" check -
    /// run this after every delivery, not just when something looks off.
    /// Reports every file extension seen per category folder (spot-checks
    /// for misfiled/junk content) and flags any Year/Month folder whose
    /// shape doesn't match the current threshold rules (flat under
    /// MonthSplitThreshold, Month folders above it, half-month split above
    /// MonthHalfSplitThreshold). Metadata-only, safe against a NAS mount.
    /// </summary>
    Task<DeliveryStructureReport> VerifyDeliveryStructureAsync(string libraryPath, CancellationToken cancellationToken = default);
}
