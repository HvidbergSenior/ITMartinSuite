using FluentAssertions;
using ITMartinBudget.Application.Helpers;

namespace ITMartinBudget.Tests.Helpers;

// Covers "All gasstations in one cat. All dagligvarebutikker in one cat" -
// different brands of the same broad spending kind, which have no shared
// substring for CategoryDuplicateFinder's prefix matching to find.
[TestFixture]
public class KnownBrandCategoryGrouperTests
{
    [Test]
    public void Groups_different_gas_station_brands_into_Benzin()
    {
        var names = new List<string>
        {
            "Debetkort    Shell Service - 92",
            "Debetkort    Q8 Service - 8015",
            "Debetkort    Uno-X EV 51017 Sdr",
            "DK-NOTAZ5486 INGO ÅRHUS, RANDER",
            "Husleje",
        };

        var groups = KnownBrandCategoryGrouper.FindGroups(names);

        groups.Should().ContainSingle(g => g.SuggestedTargetName == "Benzin");
        var benzin = groups.Single(g => g.SuggestedTargetName == "Benzin");
        benzin.Names.Should().HaveCount(4);
        benzin.Names.Should().NotContain("Husleje");
    }

    [Test]
    public void Groups_different_grocery_brands_into_Dagligvarer()
    {
        var names = new List<string>
        {
            "MobilePay: Rema 1000",
            "Debetkort    Netto Skovvejen Aa",
            "Debetkort    foetex food Guldsm",
            "Debetkort    Bilka Tilst",
        };

        var groups = KnownBrandCategoryGrouper.FindGroups(names);

        groups.Should().ContainSingle(g => g.SuggestedTargetName == "Dagligvarer");
        groups[0].Names.Should().HaveCount(4);
    }

    [Test]
    public void A_single_matching_category_is_not_reported_as_a_group()
    {
        var names = new List<string> { "Debetkort    Shell Service - 92", "Husleje" };

        KnownBrandCategoryGrouper.FindGroups(names).Should().BeEmpty();
    }

    [Test]
    public void Unrelated_categories_are_never_grouped()
    {
        var names = new List<string> { "Husleje", "Forsikring", "Løn" };

        KnownBrandCategoryGrouper.FindGroups(names).Should().BeEmpty();
    }
}
