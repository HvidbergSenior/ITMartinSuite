using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IImageAnalysisService
{
    Task<AiAnalysisResult> AnalyzeImageAsync(
        string filePath);
}