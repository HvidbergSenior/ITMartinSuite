using FluentAssertions;
using ITMartinElPriser.Domain;
using ITMartinElPriser.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace ITMartinElPriser.Tests;

/// <summary>The real chain: address (OpenStreetMap) -> grid company (Green Power Denmark) -> household tariff
/// (Energinet DatahubPricelist). Needs internet and takes up to ~2 minutes (Energi Data Service rate limit).
/// Run: dotnet test --filter "Category=Integration"</summary>
[TestFixture]
[Category("Integration")]
public class GridLookupLiveTests
{
    [Test]
    public async Task Skelagervej_222_is_Konstant_DK1_with_a_winter_tariff()
    {
        var grids = new GridTariffs(new HttpClient { Timeout = TimeSpan.FromSeconds(120) }, NullLogger<GridTariffs>.Instance);
        if (!await grids.WaitLoadedAsync(TimeSpan.FromMinutes(5))) Assert.Ignore("Energi Data Service not reachable");
        var place = await grids.ResolveAsync("Skelagervej 222, 8200 Aarhus N");
        if (place is null) Assert.Ignore("OpenStreetMap not reachable");
        var g = await grids.FindAtAsync(place!);
        if (g is null) Assert.Ignore("Green Power Denmark not reachable");
        TestContext.Out.WriteLine($"{place!.Label} [{place.Region}] -> {g!.Id} / {g.Name} / {g.PriceArea}");
        g.Id.Should().Be("Konstant Net A/S - 151");
        g.PriceArea.Should().Be("DK1");

        var today = await grids.TodayAsync(g.Id);
        TestContext.Out.WriteLine(string.Join(" ", today.Select(t => $"{t.Hour}:{t.OreInclVat}")));
        today.Should().HaveCount(24);
        today.Single(t => t.Hour == 18).OreInclVat.Should().BeGreaterThan(today.Single(t => t.Hour == 3).OreInclVat);

        var kbh = await grids.ResolveAsync("Rådhuspladsen 1, 1550 København");
        if (kbh is not null && await grids.FindAtAsync(kbh) is { } r)
        {
            TestContext.Out.WriteLine($"{kbh.Label} [{kbh.Region}] -> {r.Id} / {r.PriceArea}");
            r.PriceArea.Should().Be("DK2");
        }
    }
}
