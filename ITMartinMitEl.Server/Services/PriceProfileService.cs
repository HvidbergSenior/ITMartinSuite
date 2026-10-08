using ITMartinElPriser.Infrastructure;
using ITMartinElPriser.Core;

namespace ITMartinMitEl.Server.Services;

// The "typical day" curve: hour-by-hour average over the last two weeks of
// published prices. Built once and kept for six hours - the days it is made
// of never change, only the window slides.
public sealed class PriceProfileService(ElectricityPriceService prices, HouseholdStore store)
{
    public const int Days = 14;
    private readonly object _lock = new();
    private (string Key, HourProfile Profile, int DaysUsed, DateTime BuiltUtc)? _cached;

    public async Task<(HourProfile Profile, int DaysUsed)> GetTypicalAsync(CancellationToken ct = default)
    {
        var s = store.Get().Settings;
        var key = $"{s.PriceArea}|{s.ShowAllIn}|{s.GridCompanyId}|{s.SupplierId}|{s.CustomNettarifOre}|{s.CustomTillaegOre}";
        lock (_lock)
            if (_cached is { } c && c.Key == key && DateTime.UtcNow - c.BuiltUtc < TimeSpan.FromHours(6))
                return (c.Profile, c.DaysUsed);

        var today = DateOnly.FromDateTime(DkTime.Now);
        var range = await prices.GetRangeAsync(today.AddDays(-Days), today, s.PriceArea, ct);
        var all = range.Select(p => PriceBreakdownCalculator.Compute(p, s)).ToList();
        var used = all.Select(p => DateOnly.FromDateTime(p.TimeDk)).Distinct().Count();
        var profile = HourProfile.FromPoints(all, s.ShowAllIn);
        if (used > 0) lock (_lock) _cached = (key, profile, used, DateTime.UtcNow);
        return (profile, used);
    }
}
