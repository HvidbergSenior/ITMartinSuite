using System.Collections.Concurrent;
using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Domain.Interfaces;
using ITMartin.Media.Domain.Models;

namespace ITMartin.Media.Infrastructure.Ai;

public sealed class InMemoryAiCacheService
    : IAiCacheService
{
    private readonly ConcurrentDictionary<
        string,
        AiCacheItem> _cache = new();

    public Task<AiCacheItem?> GetAsync(
        string hash)
    {
        _cache.TryGetValue(
            hash,
            out var result);

        return Task.FromResult(result);
    }

    public Task SaveAsync(
        string hash,
        AiAnalysisResult result)
    {
        _cache[hash] = new AiCacheItem
        {
            Hash = hash,
            Description = result.Description,
            Tags = result.Tags,
            Confidence = result.Confidence,
            CreatedAt = DateTime.UtcNow
        };

        return Task.CompletedTask;
    }
}