// File: ITMartin.Media.Domain.Interfaces/IAiCacheService.cs

using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Entities;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

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