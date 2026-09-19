using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using ITMartinElPriser.Core;
using ITMartinMitEl.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ITMartinElPriser.Tests;

/// <summary>
/// Cross-checks our numbers against two things we do not control:
///
///  1. elprisenligenu.dk - an independent public site that publishes the
///     same day-ahead spot prices (hourly, DKK/kWh). If our hourly averages
///     drift from theirs, we are reading Energinet wrong (unit, timezone,
///     dataset) - exactly the failure that made the old app show a fake
///     curve for weeks without anyone noticing.
///  2. The deployed app's own /api/snapshot - so the live container agrees
///     with the code under test.
///
/// Needs internet; skipped (not failed) when either side is unreachable.
/// Run: dotnet test --filter "Category=Integration"
/// </summary>
[TestFixture]
[Category("Integration")]
public class ReferenceApiTests
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    private sealed record RefPrice(
        [property: JsonPropertyName("DKK_per_kWh")] double DkkPerKwh,
        [property: JsonPropertyName("time_start")] DateTimeOffset TimeStart);

    private static async Task<List<RefPrice>> ReferenceAsync(DateOnly date, string area)
    {
        var url = $"https://www.elprisenligenu.dk/api/v1/prices/{date:yyyy}/{date:MM-dd}_{area}.json";
        try
        {
            return await Http.GetFromJsonAsync<List<RefPrice>>(url) ?? [];
        }
        catch (Exception ex)
        {
            Assert.Ignore($"OFFLINE - elprisenligenu.dk unreachable: {ex.Message}");
            return [];
        }
    }

    private static async Task<List<PricePoint>> OursAsync(string area)
    {
        var svc = new ElectricityPriceService(Http, NullLogger<ElectricityPriceService>.Instance);
        var prices = await svc.GetPricesAsync(area);
        if (prices.Count == 0) Assert.Ignore("OFFLINE - Energinet returned nothing");
        return prices;
    }

    [TestCase("DK1")]
    [TestCase("DK2")]
    public async Task Todays_hourly_spot_prices_match_elprisenligenu(string area)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var reference = await ReferenceAsync(today, area);
        var ours = await OursAsync(area);

        reference.Should().HaveCount(24, "the reference publishes one price per hour");

        var ourHourly = ours
            .Where(p => DateOnly.FromDateTime(p.TimeDk) == today)
            .GroupBy(p => p.TimeDk.Hour)
            .ToDictionary(g => g.Key, g => g.Average(p => p.PriceKrPerKwh));

        ourHourly.Should().HaveCount(24, "we should have every hour of today from Energinet");

        foreach (var r in reference)
        {
            var hour = r.TimeStart.ToOffset(TimeSpan.FromHours(r.TimeStart.Offset.Hours)).Hour;
            ourHourly.Should().ContainKey(hour);
            // Both sides quote Energinet's DKK figure; the reference rounds to
            // 5 decimals and averages 15-minute slots the same way, so a few
            // øre is already generous.
            ourHourly[hour].Should().BeApproximately(r.DkkPerKwh, 0.02,
                $"{area} hour {hour:00} should agree with the reference site");
        }
    }

    [Test]
    public async Task Tomorrows_prices_match_when_both_sides_have_them()
    {
        var tomorrow = DateOnly.FromDateTime(DateTime.Now).AddDays(1);
        var ours = (await OursAsync("DK1")).Where(p => DateOnly.FromDateTime(p.TimeDk) == tomorrow).ToList();
        if (ours.Count == 0) Assert.Ignore("Tomorrow not published yet (comes ~13:00)");

        var reference = await ReferenceAsync(tomorrow, "DK1");
        if (reference.Count == 0) Assert.Ignore("Reference site does not have tomorrow yet");

        var ourMin = ours.GroupBy(p => p.TimeDk.Hour).Min(g => g.Average(p => p.PriceKrPerKwh));
        var refMin = reference.Min(r => r.DkkPerKwh);
        ourMin.Should().BeApproximately(refMin, 0.02, "the cheapest hour tomorrow must be the same on both sides");
    }

    [Test]
    public async Task Deployed_app_snapshot_agrees_with_this_code()
    {
        var url = Environment.GetEnvironmentVariable("ELPRISER_URL") ?? "https://elpriser.itmartin.dk";
        Snapshot? live;
        try
        {
            live = await Http.GetFromJsonAsync<Snapshot>($"{url}/api/snapshot");
        }
        catch (Exception ex)
        {
            Assert.Ignore($"OFFLINE - {url} unreachable: {ex.Message}");
            return;
        }

        live!.HasData.Should().BeTrue("the deployed app should have live prices, not a blank");
        live.NowKrPerKwh.Should().BeGreaterThan(0);

        // Rebuild the same snapshot locally with the deployed app's settings
        // (all-in on/off matters) - spot-only comparison keeps it independent
        // of grid/supplier presets that might differ between here and there.
        var ours = await OursAsync("DK1");
        var local = PriceModel.Build(ours, new HouseholdSettings { ShowAllIn = false }, Appliance.Defaults(), DateTime.Now);
        var liveSpotNow = live.AllIn ? null : live.NowKrPerKwh;
        if (liveSpotNow is { } s)
            local.NowKrPerKwh.Should().BeApproximately(s, 0.001);

        live.Today.Should().NotBeNull();
        live.Today!.Hours.Should().HaveCount(24);
        live.Today.Hours.Select(h => h.Start.Hour).Should().BeEquivalentTo(Enumerable.Range(0, 24));
    }

    private sealed record Snapshot(bool HasData, bool AllIn, double? NowKrPerKwh, Day? Today);
    private sealed record Day(List<Hour> Hours);
    private sealed record Hour(DateTime Start, double KrPerKwh);
}
