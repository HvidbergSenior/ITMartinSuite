using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Domain.Models;

namespace ITMartin.Media.Domain.Interfaces;

public interface IAiCacheService
{
    Task<AiCacheItem?> GetAsync(string hash);

    Task SaveAsync(
        string hash,
        AiAnalysisResult result);
}