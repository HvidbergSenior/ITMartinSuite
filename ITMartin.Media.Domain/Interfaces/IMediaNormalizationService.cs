using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Domain.Interfaces;

public interface IMediaNormalizationService
{
    Task NormalizeAsync(
        List<MediaFile> files,
        Func<int, int, string, Task>? progress = null);
}