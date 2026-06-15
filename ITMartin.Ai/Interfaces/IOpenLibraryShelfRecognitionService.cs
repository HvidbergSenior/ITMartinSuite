using ITMartin.Ai.Models;

namespace ITMartin.Ai.Interfaces;

public interface IOpenAiLibraryShelfRecognitionService
{
    Task<LibraryShelfAnalysisResult?>
        AnalyzeAsync(
            string filePath,
            CancellationToken cancellationToken);
}