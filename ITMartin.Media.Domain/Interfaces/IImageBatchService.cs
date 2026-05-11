using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Interfaces;

public interface IImageBatchService
{
    Task ConvertAllImagesAsync(
        IEnumerable<MediaFile> files,
        Action<int, int, string>? progress = null);
}