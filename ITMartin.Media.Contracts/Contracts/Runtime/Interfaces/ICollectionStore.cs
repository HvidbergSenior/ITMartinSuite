using ITMartin.Media.Contracts.Entities;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface ICollectionStore
{
    /// <summary>
    /// collections.json lives inside libraryPath itself, not a fixed global
    /// config - each gallery/library gets its own file, so this must be scoped
    /// per-call rather than fixed at DI-construction time.
    /// </summary>
    Task<List<MediaCollection>> LoadAsync(string libraryPath);
    Task SaveAsync(string libraryPath, List<MediaCollection> collections);
}
