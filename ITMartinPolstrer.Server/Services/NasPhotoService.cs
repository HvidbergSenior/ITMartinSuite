using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

namespace ITMartinPolstrer.Server.Services;

// Pushes uploaded photos/videos straight to the NAS gallery - "just pictures
// directly to nas" (2026-09-08) - reusing the exact same push/wire machinery
// FileSorter already uses for family photo galleries (INasDeliveryService),
// rather than storing anything locally long-term. One persistent gallery
// ("polstrer") for every job; each job gets its own subfolder inside it, so
// a single bookmarked link always shows everything, old and new.
public sealed class NasPhotoService(INasDeliveryService nasDelivery, ILogger<NasPhotoService> logger)
{
    private const string GallerySlug = "polstrer";
    private const string GalleryDisplayName = "Møbelpolstrer";

    // No separate secret to configure - same convention agreed for FileSorter
    // client galleries (2026-09-08): the folder name plus the year.
    private static string GalleryPassword => $"{GallerySlug}{DateTime.UtcNow.Year}";

    public sealed record PendingFile(string OriginalFileName, Stream Content);

    // Stages every selected file for one job under a single temp folder
    // shaped like the NAS target (polstrer/<jobSlug>/<file>), then pushes
    // that folder ONCE - PushToNasAsync merges additively, so this adds
    // only the new files without touching any other job's existing photos.
    // Doing one push per file here would mean one tar/scp/ssh round trip
    // per photo, which is slow and hammers the NAS for nothing - a whole
    // selection (she'll often pick several at once) goes in one push, same
    // "batch, don't loop" principle as this suite already applies to AI
    // calls.
    public async Task<string?> UploadAsync(string jobSlug, IReadOnlyList<PendingFile> files, CancellationToken cancellationToken = default)
    {
        if (files.Count == 0) return null;

        var stagingRoot = Path.Combine(Path.GetTempPath(), $"polstrer-upload-{Guid.NewGuid():N}");
        var jobDir = Path.Combine(stagingRoot, jobSlug);
        Directory.CreateDirectory(jobDir);

        try
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            foreach (var file in files)
            {
                // A short per-file random suffix, not just the shared batch
                // timestamp, since several files in the same batch would
                // otherwise all collide on the same second.
                var safeName = $"{timestamp}-{Guid.NewGuid().ToString("N")[..6]}-{Path.GetFileName(file.OriginalFileName)}";
                var localPath = Path.Combine(jobDir, safeName);

                await using var fileStream = new FileStream(localPath, FileMode.Create);
                await file.Content.CopyToAsync(fileStream, cancellationToken);
            }

            var pushResult = await nasDelivery.PushToNasAsync(stagingRoot, GallerySlug, cancellationToken);
            if (!pushResult.Success)
            {
                logger.LogError("Push to NAS failed for job {JobSlug}: {Error}", jobSlug, pushResult.Error);
                return pushResult.Error;
            }

            var wireResult = await nasDelivery.WireGalleryAsync(GallerySlug, GalleryDisplayName, GalleryPassword, cancellationToken);
            if (!wireResult.Success)
            {
                logger.LogError("Gallery wiring failed: {Error}", wireResult.Error);
                return wireResult.Error;
            }

            return null;
        }
        finally
        {
            try { Directory.Delete(stagingRoot, recursive: true); } catch { /* best effort */ }
        }
    }
}
