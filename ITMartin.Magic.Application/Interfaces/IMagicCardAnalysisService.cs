using ITMartin.Magic.Application.Models;
using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Domain.Models;

namespace ITMartin.Magic.Application.Interfaces;

public interface IMagicCardAnalysisService
{
    Task<MagicCardAnalysisResult?> AnalyzeMagicCardAsync(
        string filePath,
        CardDetectionResult detection);

    Task<CardConditionResult?> AnalyzeCardConditionAsync(
        string filePath,
        decimal? eurPrice,
        decimal? usdPrice);
}