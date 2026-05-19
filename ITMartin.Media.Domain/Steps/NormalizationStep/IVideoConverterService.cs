namespace ITMartin.Media.Domain.Steps.NormalizationStep;

public interface IVideoConverterService
{
    Task<string?> ConvertToUniversalMp4Async(
        string inputPath,
        string outputFolder);
}