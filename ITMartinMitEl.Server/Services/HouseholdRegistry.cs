using System.Collections.Concurrent;
using ITMartinMitEl.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace ITMartinMitEl.Server.Services;

/// <summary>
/// Hands out the one <see cref="HouseholdStore"/> and <see cref="ConsumptionStore"/> per
/// household and writes their json back to the database. Caching the instances keeps the
/// old in-memory behaviour (a page reads without touching the disk) while the data itself
/// is now per account; the lock per household means two browser tabs cannot interleave a write.
/// </summary>
public sealed class HouseholdRegistry(IDbContextFactory<MitElDbContext> factory)
{
    private readonly ConcurrentDictionary<Guid, HouseholdStore> _households = new();
    private readonly ConcurrentDictionary<Guid, ConsumptionStore> _consumption = new();

    public HouseholdStore Household(Guid householdId) =>
        _households.GetOrAdd(householdId, id =>
        {
            using var db = factory.CreateDbContext();
            var row = db.Households.Find(id);
            return new HouseholdStore(id, row?.DataJson, SaveHouseholdJson);
        });

    public ConsumptionStore Consumption(Guid householdId) =>
        _consumption.GetOrAdd(householdId, id =>
        {
            using var db = factory.CreateDbContext();
            var row = db.Households.Find(id);
            return new ConsumptionStore(id, row?.ConsumptionJson, SaveConsumptionJson);
        });

    /// <summary>Every household, for the jobs that must run for all of them (meter sync, notifications).</summary>
    public List<Guid> AllHouseholdIds()
    {
        using var db = factory.CreateDbContext();
        return db.Households.Select(h => h.Id).ToList();
    }

    public string HouseholdName(Guid householdId)
    {
        using var db = factory.CreateDbContext();
        return db.Households.Find(householdId)?.Name ?? "kunden";
    }

    /// <summary>Drops the cached instances - used after an account (and its household) is deleted.</summary>
    public void Forget(Guid householdId)
    {
        _households.TryRemove(householdId, out _);
        _consumption.TryRemove(householdId, out _);
    }

    private void SaveHouseholdJson(Guid householdId, string json)
    {
        if (DemoHousehold.IsDemo(householdId)) return;   // the demo is read-only

        using var db = factory.CreateDbContext();
        var row = db.Households.Find(householdId);
        if (row is null) return;          // deleted while a page still had it open
        row.DataJson = json;
        db.SaveChanges();
    }

    private void SaveConsumptionJson(Guid householdId, string json)
    {
        if (DemoHousehold.IsDemo(householdId)) return;
        using var db = factory.CreateDbContext();
        var row = db.Households.Find(householdId);
        if (row is null) return;
        row.ConsumptionJson = json;
        db.SaveChanges();
    }
}
