namespace ITMartin.Media.Domain.Steps.NormalizationStep;

public interface IImageConverterService
{
    bool NeedsConversion(string path);
    bool ShouldKeepOriginal(string path);
    Task<string?> ConvertToJpgAsync(string inputPath);
}