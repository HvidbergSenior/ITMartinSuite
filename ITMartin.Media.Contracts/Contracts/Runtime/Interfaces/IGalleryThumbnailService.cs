namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

// gallery-web's live /api/browse looks for a "thumbnails" subfolder next to
// each media file (see Gallery.Server's Thumb()/FolderCover() helpers) and
// falls back to serving the full-resolution original if it's missing - slow
// to load in a grid. This generates that per-folder thumbnails structure,
// distinct from StaticGalleryExportService's centralized _Galleri/thumbs
// (which is for the offline physical-drive export, not live browsing).
public interface IGalleryThumbnailService
{
    Task<int> GenerateAsync(string libraryPath, CancellationToken cancellationToken = default);
}
