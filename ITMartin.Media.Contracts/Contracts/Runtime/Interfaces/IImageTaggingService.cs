using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

// AI (Claude Haiku) image tagging for the "Søgning & mærker" add-on - tags get
// written back into manifest.json's MediaFile.AiTags so Gallery.Server's
// /api/search can filter by them without any new storage. Only ever run
// manually per gallery (same discipline as Rejser/Årbog/Efter person) - never
// automatic, since a real library can run into the tens of thousands of files
// and this costs a real (if small, Haiku-priced) amount per photo.
public interface IImageTaggingService
{
    /// <summary>
    /// Tags every image in the library that doesn't already have tags. Safe to
    /// call again and again, including after new photos have been added -
    /// already-tagged files are skipped, so a re-run only costs whatever's new.
    /// </summary>
    Task<ImageTaggingResult> TagLibraryAsync(string libraryPath, CancellationToken cancellationToken = default);
}
