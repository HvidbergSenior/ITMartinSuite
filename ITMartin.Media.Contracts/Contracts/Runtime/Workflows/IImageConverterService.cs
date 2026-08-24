namespace ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

public interface IImageConverterService
{
    bool NeedsConversion(string path);
    bool ShouldKeepOriginal(string path);
    Task<string?> ConvertToJpgAsync(string inputPath);

    // Cheap, decode-free EXIF tag read - true whenever the source file
    // carries a usable Orientation tag (any value 1-8, including "1 = already
    // upright"), meaning the correct orientation is known without ever
    // needing the expensive face-detection fallback. False only means no
    // tag was present at all - genuinely unknown, not "confirmed wrong".
    bool TryGetSourceOrientation(string path, out ushort orientation);
}