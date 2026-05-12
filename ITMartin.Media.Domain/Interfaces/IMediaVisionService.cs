using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Domain.Interfaces;

public interface IMediaVisionService
{
    Task ProcessAsync(
        List<MediaFile> files,
        Func<int, int, string, Task>? progress = null);
}