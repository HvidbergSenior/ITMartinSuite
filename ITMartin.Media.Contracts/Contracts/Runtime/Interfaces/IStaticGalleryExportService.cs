using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

// Generates a fully self-contained, offline-browsable HTML gallery directly inside
// the library folder - real JPEG thumbnails + static pages linking back to the
// original files by relative path. No server, NAS, or internet connection needed
// to browse it. Meant for final delivery (e.g. copying the finished library onto
// an external hard drive), where SmartFolders' symlink-based approach breaks once
// the folder is moved off the machine that created the links.
public interface IStaticGalleryExportService
{
    Task<StaticGalleryExportResult> ExportAsync(string libraryPath, CancellationToken cancellationToken = default);
}
