using ITMartin.Ai.Models;

namespace ITMartin.Ai.Interfaces;

public interface ICdRecognitionService
{
    Task<CdRecognitionResult?> AnalyzeAsync(string filePath, CancellationToken cancellationToken = default);
}
