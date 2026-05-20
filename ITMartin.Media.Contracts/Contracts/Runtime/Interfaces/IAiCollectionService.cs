using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Entities;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IAiCollectionService
{
    List<AiCollection> BuildCollections(
        List<MediaFile> files);
}