using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Models;
using ITMartin.Magic.Domain.Entities;
using ITMartin.Magic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ITMartin.Magic.Infrastructure.Services;

public sealed class MagicKnowledgeService
    : IMagicKnowledgeService
{
    private readonly MagicDbContext _db;
    private readonly IMemoryCache   _cache;
    private const string SetsCacheKey = "magic_set_definitions";

    public MagicKnowledgeService(MagicDbContext db, IMemoryCache cache)
    {
        _db    = db;
        _cache = cache;
    }

    public async Task<MagicKnowledgeDashboardModel>
        GetDashboardAsync()
    {
        var total =
            await _db.Sets.CountAsync();

        var known =
            await _db.Sets.CountAsync(
                x => !string.IsNullOrWhiteSpace(
                    x.SymbolDescription));

        return new MagicKnowledgeDashboardModel
        {
            TotalSets = total,

            KnownSymbols = known,

            MissingSymbols =
                total - known,

            CoveragePercent =
                total == 0
                    ? 0
                    : known * 100m / total,

            MissingKnowledge =
                await _db.Sets
                    .Where(x =>
                        string.IsNullOrWhiteSpace(
                            x.SymbolDescription))
                    .OrderBy(x => x.SetName)
                    .Take(100)
                    .ToListAsync(),

            KnownKnowledge =
                await _db.Sets
                    .Where(x =>
                        !string.IsNullOrWhiteSpace(
                            x.SymbolDescription))
                    .OrderBy(x => x.SetName)
                    .ToListAsync()
        };
    }

    public Task<MagicSetKnowledge?>
        GetAsync(
            string setCode)
    {
        return _db.Sets
            .FirstOrDefaultAsync(
                x => x.SetCode == setCode);
    }

    public async Task UpdateAsync(
        MagicSetKnowledge set)
    {
        _db.Update(set);

        await _db.SaveChangesAsync();
    }
    public async Task<List<MagicSetSymbolDefinition>> GetSetDefinitionsAsync()
    {
        if (_cache.TryGetValue(SetsCacheKey, out List<MagicSetSymbolDefinition>? cached) && cached is not null)
            return cached;

        var sets = await _db.Sets
            .OrderBy(x => x.SetName)
            .Select(x => new MagicSetSymbolDefinition(
                x.SetCode,
                x.SetName,
                x.SymbolDescription ?? string.Empty,
                x.IconSvgUri ?? string.Empty))
            .ToListAsync();

        _cache.Set(SetsCacheKey, sets, TimeSpan.FromHours(1));
        return sets;
    }
}