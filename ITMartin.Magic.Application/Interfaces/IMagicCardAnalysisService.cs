using ITMartin.Magic.Application.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Entities;

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