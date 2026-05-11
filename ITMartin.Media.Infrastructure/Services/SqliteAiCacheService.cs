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
    private readonly IDbContextFactory<MediaDbContext> _dbFactory;

    public SqliteAiCacheService(
        IDbContextFactory<MediaDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<AiCacheItem?> GetAsync(
        string hash)
    {
        await using var db =
            await _dbFactory.CreateDbContextAsync();

        var entity =
            await db.AiCache
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
        await using var db =
            await _dbFactory.CreateDbContextAsync();

        var exists =
            await db.AiCache
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

        db.AiCache.Add(entity);

        await db.SaveChangesAsync();
    }
}