// File: ITMartin.Media.Domain.Interfaces/IAiCacheService.cs

using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Domain.Models;

namespace ITMartin.Media.Domain.Interfaces;

public interface IAiCacheService
{
    Task<AiCacheItem?> GetAsync(
        string hash,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        string hash,
        AiAnalysisResult result,
        CancellationToken cancellationToken = default);
}