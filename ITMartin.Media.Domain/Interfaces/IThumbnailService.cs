using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Domain.Interfaces;

public interface IThumbnailService
{
    string? GenerateThumbnail(MediaFile file);
}