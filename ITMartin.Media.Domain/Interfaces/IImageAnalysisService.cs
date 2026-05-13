using ITMartin.Media.Domain.Models;

namespace ITMartin.Media.Domain.Interfaces;

public interface IImageAnalysisService
{
    Task<AiAnalysisResult> AnalyzeImageAsync(
        string filePath);
}