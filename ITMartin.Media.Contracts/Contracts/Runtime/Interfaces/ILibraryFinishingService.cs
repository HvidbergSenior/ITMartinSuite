using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

// Runs every free/automatic post-sort phase against an already-QuickSorted
// library, in the dependency-correct order (reorganize -> index converge ->
// dedup -> SmartFolders add-ons -> gallery export -> delivery verify), then
// writes a human-readable Markdown report into the library itself.
//
// Deliberately excludes anything that costs real Claude API money per file
// (ReclassifyScreenshotsAsync, FixOrientationAsync's paid fallback,
// EstimateUndatedPhotoYearsAsync) or needs a human to name something first
// (GeneratePersonFolderAsync, PickBestShotsAsync - explicitly "admin-
// triggered only" per its own doc comment) - those stay manual, run by hand
// via their existing /api/debug/* endpoints, never silently bundled into an
// "automatic overnight run."
public interface ILibraryFinishingService
{
    Task<LibraryFinishingReport> RunAsync(string libraryPath, CancellationToken cancellationToken = default);
}
