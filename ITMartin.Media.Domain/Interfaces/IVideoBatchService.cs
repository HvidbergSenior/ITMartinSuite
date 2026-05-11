using ITMartin.Media.Entities;

namespace ITMartin.Media.Interfaces;

public interface IVideoBatchService
{
    Task ConvertAllVideosAsync(
        IEnumerable<MediaFile> files,
        Action<int, int, string>? progress = null);
}