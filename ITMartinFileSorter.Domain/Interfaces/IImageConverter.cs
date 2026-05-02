namespace ITMartinFileSorter.Domain.Interfaces;

public interface IImageConverter
{
    bool NeedsConversion(string path);
    bool ShouldKeepOriginal(string path);
    Task<string?> ConvertToJpgAsync(string inputPath);
}