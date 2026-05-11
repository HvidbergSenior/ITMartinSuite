using System.Text.Json;
using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Domain.Interfaces;
using ITMartin.Media.Domain.Models;
using ITMartin.Media.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartin.Media.Infrastructure.Services;

public sealed class SqliteAiCacheService
    : IAiCacheService
{
    private readonly MediaDbContext _db;

    public SqliteAiCacheService(
        MediaDbContext db)
    {
        _db = db;
    }

    public async Task<AiCacheItem?> GetAsync(
        string hash)
    {
        var entity =
            await _db.AiCache
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Hash == hash);

        if (entity == null)
            return null;

        return new AiCacheItem
        {
            Hash = entity.Hash,
            Description = entity.Description,
            Confidence = entity.Confidence,
            CreatedAt = entity.CreatedAt,
            Tags = JsonSerializer.Deserialize<
                List<string>>(
                entity.TagsJson) ?? []
        };
    }

    public async Task SaveAsync(
        string hash,
        AiAnalysisResult result)
    {
        var exists =
            await _db.AiCache
                .AnyAsync(x => x.Hash == hash);

        if (exists)
            return;

        var entity = new AiCache
        {
            Hash = hash,
            Description = result.Description,
            Confidence = result.Confidence,
            CreatedAt = DateTime.UtcNow,
            TagsJson = JsonSerializer.Serialize(
                result.Tags)
        };

        _db.AiCache.Add(entity);

        await _db.SaveChangesAsync();
    }
}