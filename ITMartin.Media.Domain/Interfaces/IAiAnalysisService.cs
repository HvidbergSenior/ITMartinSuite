using ITMartin.Media.Domain.Models;

namespace ITMartin.Media.Domain.Interfaces;

public interface IAiAnalysisService
{
    Task<AiAnalysisResult> AnalyzeImageAsync(string filePath);
    
}

