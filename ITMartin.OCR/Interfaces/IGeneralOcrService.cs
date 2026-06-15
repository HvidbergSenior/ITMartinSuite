namespace ITMartin.OCR.Interfaces;

public interface IGeneralOcrService
{
    Task<string?> ExtractTextAsync(
        string imagePath,
        CancellationToken cancellationToken = default);
}