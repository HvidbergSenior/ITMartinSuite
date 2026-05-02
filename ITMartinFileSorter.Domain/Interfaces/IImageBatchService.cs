namespace ITMartinFileSorter.Domain.Interfaces;

public interface IImageBatchService
{
    Task ConvertAllImagesAsync(
        string exportRoot,
        Action<int, int, string>? progress = null);
}