using ITMartin.Ai.Models;

namespace ITMartin.Ai.Interfaces;

public interface ICardConditionAnalysisService
{
    Task<CardConditionResult?>
        AnalyzeAsync(
            string filePath,
            decimal? eurPrice,
            decimal? usdPrice, CancellationToken cancellationToken);
}