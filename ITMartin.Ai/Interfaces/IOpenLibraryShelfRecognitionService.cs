using ITMartin.Ai.Models;

namespace ITMartin.Ai.Interfaces;

public interface ILibraryShelfRecognitionService
{
    Task<LibraryShelfAnalysisResult?>
        AnalyzeAsync(
            string filePath,
            CancellationToken cancellationToken);
}