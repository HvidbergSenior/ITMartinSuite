// File: ITMartin.Media.Infrastructure.Services/SqliteAiCacheService.cs

using System.Text.Json;
using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Domain.Interfaces;
using ITMartin.Media.Domain.Models;
using ITMartin.Media.Infrastructure.Entities;
using ITMartin.Media.Infrastructure.Persistence;
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
        string hash,
        CancellationToken cancellationToken = default)
    {
        await using var db =
            await _dbFactory.CreateDbContextAsync(
                cancellationToken);

        var entity =
            await db.AiCache
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Hash == hash,
                    cancellationToken);

        if (entity == null)
        {
            return null;
        }

        return new AiCacheItem
        {
            Hash = entity.Hash,
            Description = entity.Description,
            Confidence = entity.Confidence,
            CreatedAt = entity.CreatedAt,
            Tags =
                JsonSerializer.Deserialize<
                    List<string>>(
                    entity.TagsJson)
                ?? []
        };
    }

    public async Task SaveAsync(
        string hash,
        AiAnalysisResult result,
        CancellationToken cancellationToken = default)
    {
        await using var db =
            await _dbFactory.CreateDbContextAsync(
                cancellationToken);

        var exists =
            await db.AiCache
                .AnyAsync(
                    x => x.Hash == hash,
                    cancellationToken);

        if (exists)
        {
            return;
        }

        var entity = new AiCache
        {
            Hash = hash,
            Description = result.Description,
            Confidence = result.Confidence,
            CreatedAt = DateTime.UtcNow,
            TagsJson =
                JsonSerializer.Serialize(
                    result.Tags)
        };

        db.AiCache.Add(entity);

        await db.SaveChangesAsync(
            cancellationToken);
    }
}