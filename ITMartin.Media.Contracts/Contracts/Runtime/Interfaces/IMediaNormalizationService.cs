using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IMediaNormalizationService
{
    Task NormalizeAsync(
        List<MediaFile> files,
        Func<int, int, string, Task>? progress = null);
}