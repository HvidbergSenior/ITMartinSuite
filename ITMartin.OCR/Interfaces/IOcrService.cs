namespace ITMartin.OCR.Interfaces;

public interface IOcrService
{
    Task<string?> ExtractTextAsync(string path);
}