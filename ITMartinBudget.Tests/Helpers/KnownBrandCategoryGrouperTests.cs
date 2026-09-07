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
    public void Groups_Coop365_and_7_Eleven_into_Dagligvarer_too()
    {
        // Real gap found live 2026-09-07 on Rico's ledger: "Coop365
        // Troejborg", "Coop365 Roende" and "7-Eleven 034" were sitting as
        // their own separate cards next to an already-merged "Dagligvarer"
        // group, because neither chain was in the original keyword list.
        var names = new List<string>
        {
            "Coop365 Troejborg",
            "Coop365 Roende",
            "7-Eleven 034",
            "Husleje",
        };

        var groups = KnownBrandCategoryGrouper.FindGroups(names);

        groups.Should().ContainSingle(g => g.SuggestedTargetName == "Dagligvarer");
        var dagligvarer = groups.Single(g => g.SuggestedTargetName == "Dagligvarer");
        dagligvarer.Names.Should().Contain(["Coop365 Troejborg", "Coop365 Roende", "7-Eleven 034"]);
        dagligvarer.Names.Should().NotContain("Husleje");
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
