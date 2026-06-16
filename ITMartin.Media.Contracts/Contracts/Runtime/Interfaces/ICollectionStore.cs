using ITMartin.Media.Contracts.Entities;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface ICollectionStore
{
    Task<List<MediaCollection>> LoadAsync();
    Task SaveAsync(List<MediaCollection> collections);
}
