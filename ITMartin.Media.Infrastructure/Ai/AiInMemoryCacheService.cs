// File: ITMartin.Media.Infrastructure.Services/InMemoryAiCacheService.cs

using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Entities;

namespace ITMartin.Media.Infrastructure.Ai;

public sealed class InMemoryAiCacheService
    : IAiCacheService
{
    private readonly Dictionary<string, AiCacheItem> _cache = [];

    public Task<AiCacheItem?> GetAsync(
        string hash,
        CancellationToken cancellationToken = default)
    {
        _cache.TryGetValue(
            hash,
            out var item);

        return Task.FromResult(item);
    }

    public Task SaveAsync(
        string hash,
        AiAnalysisResult result,
        CancellationToken cancellationToken = default)
    {
        _cache[hash] = new AiCacheItem
        {
            Hash = hash,
            Description = result.Description,
            Confidence = result.Confidence,
            CreatedAt = DateTime.UtcNow,
            Tags = result.Tags.ToList()
        };

        return Task.CompletedTask;
    }
}