using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Interfaces;

public interface IMediaCategorizer
{
    void Categorize(MediaFile file);
}