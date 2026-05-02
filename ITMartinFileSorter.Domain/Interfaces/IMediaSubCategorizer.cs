using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Enums;

namespace ITMartinFileSorter.Domain.Interfaces;

public interface IMediaSubCategorizer
{
    MediaType Type { get; }
    void Categorize(MediaFile file);
}