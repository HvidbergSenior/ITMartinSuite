namespace ITMartin.Media.Interfaces;

public interface IVideoConverterService
{
    Task<string?> ConvertToUniversalMp4Async(
        string inputPath,
        string outputFolder);
}