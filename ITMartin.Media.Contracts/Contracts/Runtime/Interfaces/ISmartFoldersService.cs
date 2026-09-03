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
    /// IFaceIndexService's face matching). Returns null if the person has no
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
    /// Looks at photos that never got a real date (no EXIF, no filename pattern,
    /// and the filesystem-timestamp fallback isn't trustworthy either - see
    /// MediaDateService) and asks Claude vision for a best-guess year from the
    /// photo's actual content (clothing, technology visible, image quality/era).
    /// Only moves a photo out of Undated when the model is medium/high confidence
    /// AND gives a real reason - low-confidence or "no usable clue" guesses stay
    /// put rather than mis-filing a photo into a wrong year with false precision.
    /// Moved photos land in "{year}/Ukendt måned" (never a real month - vision
    /// can only place an era, not a date). Batched (multiple photos per call),
    /// Haiku, hard-capped per run, and incremental via a decided.json sidecar in
    /// Undated so a re-run only spends on photos it hasn't already decided on.
    /// </summary>
    Task<UndatedEstimateResult> EstimateUndatedPhotoYearsAsync(string libraryPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Groups whatever's still sitting in Undated/Unhandled - after every other
    /// pass (re-dating, classification) has had a chance to move things out of
    /// there - by detected face similarity. Free and local: reuses the same
    /// FaceONNX embeddings IndexFacesAsync already computed, no Claude calls, no
    /// named Person required. Clusters are anonymous ("Ukendt person 1", "Ukendt
    /// person 2", ...) so a customer can browse "these look like the same
    /// person" in their leftover pile even before anyone's tagged a real name.
    /// Groups under 3 photos are dropped as likely noise, same floor as
    /// IFaceIndexService.DiscoverUnnamedPeopleAsync, which this reuses.
    /// </summary>
    Task<List<PersonFolderResult>> GenerateUnknownPersonFoldersAsync(string libraryPath, double threshold = 0.5, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clusters visually-similar photos (same scene/session - a burst, or several
    /// shots of the same room/backdrop) into "SmartFolders/Lignende" folders. Free,
    /// local: reuses IPerceptualHashService's dHash, clustered per containing folder
    /// with a looser Hamming threshold than exact near-duplicate detection, so it
    /// catches "same background, different moment" rather than just recompressed
    /// copies of one photo. No Claude calls. Clusters under 3 photos are dropped as
    /// noise, same floor as the unnamed-person/burst passes elsewhere in this file.
    /// </summary>
    Task<List<SimilarSceneResult>> GenerateSimilarSceneFoldersAsync(string libraryPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes the already-generated SmartFolders (People/Yearbook/Trips) into
    /// the Gallery web app's own "Samlinger" (Collections) feature - so they
    /// show as a grouped row of Danish-labeled cards on the gallery's home page,
    /// instead of requiring a click into a raw "SmartFolders" folder to find them.
    /// </summary>
    Task SyncGalleryCollectionsAsync(string libraryPath, CancellationToken cancellationToken = default);
}
