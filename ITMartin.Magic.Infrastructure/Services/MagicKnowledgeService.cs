using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Models;
using ITMartin.Magic.Domain.Entities;
using ITMartin.Magic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ITMartin.Magic.Infrastructure.Services;

public sealed class MagicKnowledgeService
    : IMagicKnowledgeService
{
    private readonly MagicDbContext _db;

    public MagicKnowledgeService(
        MagicDbContext db)
    {
        _db = db;
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
}