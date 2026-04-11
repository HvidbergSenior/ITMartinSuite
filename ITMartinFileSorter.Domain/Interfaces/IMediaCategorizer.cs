using ITMartinFileSorter.Domain.Entities;

namespace ITMartinFileSorter.Domain.Interfaces;

public interface IMediaCategorizer
{
    void Categorize(MediaFile file);
}