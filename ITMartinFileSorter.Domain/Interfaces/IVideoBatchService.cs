namespace ITMartinFileSorter.Domain.Interfaces;

public interface IVideoBatchService
{
    Task ConvertAllVideosAsync(
        string exportRoot,
        Action<int, int, string>? progress = null);
}