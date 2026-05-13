using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Domain.Models;

namespace ITMartin.Media.Domain.Interfaces;

public interface IAiAnalysisService
{
    Task<AiAnalysisResult> AnalyzeImageAsync(
        string filePath);

    Task<MagicCardAnalysisResult?>
        AnalyzeMagicCardAsync(
            string filePath);

    Task<CardConditionResult?>
        AnalyzeCardConditionAsync(
            string filePath,
            decimal? eurPrice,
            decimal? usdPrice);
}