using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Enums;

namespace ITMartin.Media.Interfaces;

public interface IMediaSubCategorizer
{
    MediaType Type { get; }
    void Categorize(MediaFile file);
}