using Microsoft.EntityFrameworkCore;

namespace ITMartinLager.Infrastructure.Persistence;

// The photos and scans fetched from Vareinfo. A context per call, like the other repositories.
public sealed class IntakeRepository(IDbContextFactory<LagerDb> dbf) : IIntakeRepository
{
    public async Task AddAsync(IEnumerable<BoxShot> shots, IEnumerable<BoxScan> scans, CancellationToken ct)
    {
        var newShots = shots.ToList();
        var newScans = scans.ToList();
        await using var db = await dbf.CreateDbContextAsync(ct);
        var shotIds = newShots.Select(s => s.VareinfoId).ToList();
        var scanIds = newScans.Select(s => s.VareinfoId).ToList();
        var haveShots = (await db.BoxShots.Where(s => shotIds.Contains(s.VareinfoId)).Select(s => s.VareinfoId).ToListAsync(ct)).ToHashSet();
        var haveScans = (await db.BoxScans.Where(s => scanIds.Contains(s.VareinfoId)).Select(s => s.VareinfoId).ToListAsync(ct)).ToHashSet();
        db.BoxShots.AddRange(newShots.Where(s => haveShots.Add(s.VareinfoId)));
        db.BoxScans.AddRange(newScans.Where(s => haveScans.Add(s.VareinfoId)));
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<BoxIntake>> PerBoxAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var shots = await db.BoxShots.GroupBy(s => s.BoxCode)
            .Select(g => new
            {
                g.Key,
                Waiting = g.Count(s => s.ReadAt == null), Wanted = g.Count(s => s.ReadAt == null && s.Wanted),
                Read = g.Count(s => s.ReadAt != null), Found = g.Sum(s => s.Found),
            }).ToListAsync(ct);
        var scans = await db.BoxScans.Where(s => s.ReadAt == null).GroupBy(s => s.BoxCode)
            .Select(g => new { g.Key, Waiting = g.Sum(s => s.Quantity), Wanted = g.Where(s => s.Wanted).Sum(s => s.Quantity) })
            .ToListAsync(ct);
        var helpers = (await db.BoxShots.Select(s => new { s.BoxCode, s.Helper })
                .Union(db.BoxScans.Select(s => new { s.BoxCode, s.Helper })).ToListAsync(ct))
            .GroupBy(x => x.BoxCode).ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => x.Helper).Where(h => h != "").Distinct().Order()));
        var codes = shots.Select(s => s.Key).Union(scans.Select(s => s.Key));
        return codes.Select(code =>
            {
                var p = shots.FirstOrDefault(s => s.Key == code);
                var s = scans.FirstOrDefault(x => x.Key == code);
                return new BoxIntake(code, p?.Waiting ?? 0, p?.Wanted ?? 0, p?.Read ?? 0, p?.Found ?? 0, s?.Waiting ?? 0, s?.Wanted ?? 0,
                    helpers.GetValueOrDefault(code) ?? "");
            })
            .OrderByDescending(b => b.BoxCode).ToList();
    }

    public async Task<int> WantReadAsync(string? boxCode, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.BoxShots.Where(s => s.ReadAt == null && !s.Wanted && (boxCode == null || s.BoxCode == boxCode))
                   .ExecuteUpdateAsync(x => x.SetProperty(s => s.Wanted, true), ct)
               + await db.BoxScans.Where(s => s.ReadAt == null && !s.Wanted && (boxCode == null || s.BoxCode == boxCode))
                   .ExecuteUpdateAsync(x => x.SetProperty(s => s.Wanted, true), ct);
    }

    public async Task<List<BoxShot>> WantedShotsAsync(int max, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.BoxShots.Where(s => s.ReadAt == null && s.Wanted).OrderBy(s => s.Id).Take(max).ToListAsync(ct);
    }

    public async Task<List<BoxScan>> WantedScansAsync(int max, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.BoxScans.Where(s => s.ReadAt == null && s.Wanted).OrderBy(s => s.Id).Take(max).ToListAsync(ct);
    }

    public async Task UpdateAsync(IEnumerable<BoxShot> shots, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.BoxShots.UpdateRange(shots);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(IEnumerable<BoxScan> scans, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.BoxScans.UpdateRange(scans);
        await db.SaveChangesAsync(ct);
    }
}
