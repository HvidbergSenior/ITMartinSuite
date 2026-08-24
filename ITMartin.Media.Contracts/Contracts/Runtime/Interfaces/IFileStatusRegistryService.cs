using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

// The "isDone" registry - one JSON sidecar per library (filestatus.json at
// the library root), keyed by content hash. Callers load once, mutate the
// in-memory dictionary as they resolve files, and save (same load-mutate-save
// shape as LibraryPolishService's rotation-checked.json/rotation-decisions.json)
// rather than round-tripping the file per lookup.
public interface IFileStatusRegistryService
{
    Task<Dictionary<string, FileStatusRecord>> LoadAsync(string libraryPath, CancellationToken cancellationToken = default);

    Task SaveAsync(string libraryPath, Dictionary<string, FileStatusRecord> registry, CancellationToken cancellationToken = default);

    FileStatusReport BuildReport(Dictionary<string, FileStatusRecord> registry);
}
