namespace ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

public interface IImageConverterService
{
    bool NeedsConversion(string path);
    bool ShouldKeepOriginal(string path);
    Task<string?> ConvertToJpgAsync(string inputPath);
}