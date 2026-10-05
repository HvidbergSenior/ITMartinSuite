using System.Text.Json;
using FluentAssertions;
using ITMartinElPriser.Core;

namespace ITMartinElPriser.Tests;

// Nominatim answers as captured 2026-10-05 (DAWA is gone): the label the confirmation line shows.
[TestFixture]
public class AddressLookupTests
{
    private static GridTariffs.OsmPlace Parse(string json) => JsonSerializer.Deserialize<GridTariffs.OsmPlace>(json)!;

    [Test]
    public void Street_address_gets_street_number_postcode_and_town()
    {
        var h = Parse("""{"lat":"55.6756280","lon":"12.5695780","address":{"road":"Rådhuspladsen","house_number":"1","postcode":"1550","city":"København","country_code":"dk"}}""");
        var p = GridTariffs.ToPlace(h, h.LatD, h.LonD)!;
        p.Label.Should().Be("Rådhuspladsen 1, 1550 København");
        p.PostalCode.Should().Be("1550");
        p.Lat.Should().BeApproximately(55.6756, 0.001);
    }

    [Test]
    public void Bare_postcode_and_village_still_give_a_label()
    {
        var h = Parse("""{"lat":"56.15","lon":"10.20","address":{"postcode":"8000","village":"Aarhus","country_code":"dk"}}""");
        GridTariffs.ToPlace(h, 56.15, 10.20)!.Label.Should().Be("8000 Aarhus");
    }

    [Test]
    public void No_postcode_means_no_place()
    {
        var h = Parse("""{"lat":"55","lon":"12","address":{"road":"Somewhere","country_code":"dk"}}""");
        GridTariffs.ToPlace(h, 55, 12).Should().BeNull();
    }

    [TestCase("Aarhus Kommune", "Aarhus")]
    [TestCase("Bornholms Regionskommune", "Bornholm")]
    [TestCase("Frederiksberg", "Frederiksberg")]
    [TestCase("Københavns Kommune", "København")]
    public void Municipality_gives_a_fallback_town(string municipality, string town) =>
        new GridTariffs.OsmAddress { Municipality = municipality }.MunicipalityTown().Should().Be(town);
}
