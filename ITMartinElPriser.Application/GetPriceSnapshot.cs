using ITMartinElPriser.Core;

namespace ITMartinElPriser.Application;

/// <summary>What strøm costs now, today and tomorrow for a household - and what each appliance costs if started now
/// versus at the cheapest time. Used by /api/snapshot (also read by itmartin.dk's "Vidste du" box) and the push test.</summary>
public sealed class GetPriceSnapshot(IPriceSource prices, TimeProvider clock)
{
    private static readonly TimeZoneInfo Dk = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

    /// <param name="area">"DK1" or "DK2" overrides the household's own area; anything else is ignored.</param>
    /// <param name="allIn">Prices with all taxes and tariffs (true) or the bare spot price (false); null keeps the setting.</param>
    public async Task<PriceSnapshot> ExecuteAsync(HouseholdSettings household, string? area, bool? allIn, IEnumerable<Appliance>? appliances, CancellationToken ct)
    {
        if (area is "DK1" or "DK2") household.PriceArea = area;
        if (allIn is { } a) household.ShowAllIn = a;
        var now = TimeZoneInfo.ConvertTimeFromUtc(clock.GetUtcNow().UtcDateTime, Dk);
        return PriceModel.Build(await prices.GetPricesAsync(household.PriceArea, ct), household, appliances ?? Appliance.Defaults(), now);
    }
}
