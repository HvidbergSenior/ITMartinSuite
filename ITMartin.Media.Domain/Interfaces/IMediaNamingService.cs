using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Domain.Interfaces;

public interface IMediaNamingService
{
    string BuildFileName(
        MediaFile file);
}