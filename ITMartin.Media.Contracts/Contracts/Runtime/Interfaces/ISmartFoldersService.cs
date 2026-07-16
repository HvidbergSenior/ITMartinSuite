using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

// Generates real, browsable folders under "<libraryPath>/SmartFolders" - AI/heuristic
// groupings that sit alongside the authoritative organized library without ever
// touching it. Every file placed there is a symlink back to the original (falling
// back to a copy only where the OS/environment can't create one), so deleting a
// smart folder never deletes real photos, and re-running regenerates it from scratch.
public interface ISmartFoldersService
{
    /// <summary>
    /// Clusters photos by date+GPS proximity into trip/vacation folders (e.g. a
    /// week of photos all taken within ~75km of each other). Free, local, EXIF-only.
    /// </summary>
    Task<List<TripFolderResult>> GenerateTripFoldersAsync(string libraryPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a folder of every photo matching an already-defined person (reuses
    /// IPackage3Service's face matching). Returns null if the person has no
    /// reference photos or no matches were found.
    /// </summary>
    Task<PersonFolderResult?> GeneratePersonFolderAsync(string libraryPath, Guid personId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a lightweight "year in review" - a folder with a representative
    /// sample of that year's photos plus a generated index.html page to browse
    /// them. No AI cost - pure date-based sampling.
    /// </summary>
    Task<YearbookResult?> GenerateYearbookAsync(string libraryPath, int year, CancellationToken cancellationToken = default);

    /// <summary>
    /// Splits geotagged photos into Home vs Away folders using the same "home"
    /// detection as trip clustering. Only covers photos that actually carry GPS -
    /// libraries with little/no location metadata will see very few files sorted.
    /// </summary>
    Task<HomeAwayResult> GenerateHomeAwayFoldersAsync(string libraryPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes the already-generated SmartFolders (Home/Outside/People/Yearbook)
    /// into the Gallery web app's own "Samlinger" (Collections) feature - so they
    /// show as a grouped row of Danish-labeled cards on the gallery's home page,
    /// instead of requiring a click into a raw "SmartFolders" folder to find them.
    /// </summary>
    Task SyncGalleryCollectionsAsync(string libraryPath, CancellationToken cancellationToken = default);
}
