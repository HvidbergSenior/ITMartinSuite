namespace ITMartinFileSorter.Domain.Interfaces;

public interface IImageBatchService
{
    Task ConvertAllImagesAsync(
        IEnumerable<MediaFile> files,
        Action<int, int, string>? progress = null);
}