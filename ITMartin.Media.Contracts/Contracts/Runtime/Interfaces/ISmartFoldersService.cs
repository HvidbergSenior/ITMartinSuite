using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

// Generates real, browsable folders under "<libraryPath>/SmartFolders" - AI/heuristic
// groupings that sit alongside the authoritative organized library without ever
// touching it. Every file placed there is a real copy, not a symlink - the
// delivered library ends up on a USB/harddisk, where a symlink's target no
// longer exists - so deleting a smart folder never deletes real photos, and
// re-running regenerates it from scratch.
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
    Task<PersonFolderResult?> GeneratePersonFolderAsync(string libraryPath, Guid personId, double threshold = 0.45, CancellationToken cancellationToken = default);

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
    /// Writes a short AI (Claude Haiku) caption per photo already in a generated
    /// Yearbook folder and rebuilds its index.html to show them - a separate,
    /// paid step from GenerateYearbookAsync itself (which is free, date-only
    /// sampling). Must be run after GenerateYearbookAsync has created the folder;
    /// returns null if it hasn't. Captions persist in a captions.json sidecar in
    /// the yearbook folder, so re-running only captions newly-added photos.
    /// </summary>
    Task<YearbookResult?> AddYearbookCaptionsAsync(string libraryPath, int year, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds burst/rapid-fire photo series (same folder, consecutive shots a
    /// few seconds apart) and uses Claude vision to pick the single best one
    /// per burst - sharpest focus, eyes open. Copies only the winners into
    /// "<libraryPath>/SmartFolders/BedsteBillede". Paid step, admin-triggered
    /// only - cost is bounded by how many real bursts exist, not library size.
    /// </summary>
    Task<BestShotResult> PickBestShotsAsync(string libraryPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Groups photos from fixed recurring calendar dates (jul, nytår) into one
    /// folder per tradition per year - so browsing reads as "Jul 2023" next to
    /// "Jul 2024" for an easy side-by-side comparison. Date-only, no AI cost,
    /// but only ever run on request like every other add-on. A tradition with
    /// photos from just one year isn't included - nothing to compare yet.
    /// </summary>
    Task<List<TraditionResult>> GenerateTraditionsAsync(string libraryPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes the already-generated SmartFolders (Home/Outside/People/Yearbook)
    /// into the Gallery web app's own "Samlinger" (Collections) feature - so they
    /// show as a grouped row of Danish-labeled cards on the gallery's home page,
    /// instead of requiring a click into a raw "SmartFolders" folder to find them.
    /// </summary>
    Task SyncGalleryCollectionsAsync(string libraryPath, CancellationToken cancellationToken = default);
}
