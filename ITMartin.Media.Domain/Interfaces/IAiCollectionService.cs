using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Domain.Models;

namespace ITMartin.Media.Domain.Interfaces;

public interface IAiCollectionService
{
    List<AiCollection> BuildCollections(
        List<MediaFile> files);
}