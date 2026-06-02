using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IMediaOcrService
{
    Task ProcessAsync(
        List<MediaFile> files,
        Func<int, int, string, Task>? progress = null, CancellationToken cancellationToken = default);
}