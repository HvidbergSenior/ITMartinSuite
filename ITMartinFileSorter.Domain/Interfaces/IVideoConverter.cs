namespace ITMartinFileSorter.Domain.Interfaces;

public interface IVideoConverter
{
    Task<string?> ConvertToUniversalMp4Async(
        string inputPath,
        string outputFolder);
}