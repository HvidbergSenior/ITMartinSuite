using ITMartin.Ai.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Ai.Interfaces;

public interface IMagicCardRecognitionService
{
    Task<MagicCardAnalysisResult?>
        AnalyzeAsync(
            string filePath,
            CardDetectionResult detection);
}