using FluentAssertions;
using ITMartinBudget.Application.Helpers;

namespace ITMartinBudget.Tests.Helpers;

// Covers real cases found live 2026-09-07 on the real Bogshoppen ledger,
// where asking AI to spot every near-duplicate in one pass over ~240
// unsorted category names missed obvious ones.
[TestFixture]
public class CategoryDuplicateFinderTests
{
    [Test]
    public void Catches_the_same_person_spread_across_bank_export_channel_prefixes_and_truncation()
    {
        var names = new List<string>
        {
            "Debetkort    Mob.Pay*Fie Tandru",
            "MobilePay MobilePay Fie Tandru",
            "MOBILEPAY FIE TANDRU",
            "MobilePay Fie Tandrup Rokkjær",
        };

        var groups = CategoryDuplicateFinder.FindGroups(names);

        groups.Should().ContainSingle();
        groups[0].Names.Should().BeEquivalentTo(names);
    }

    [Test]
    public void Catches_the_same_merchant_with_different_trailing_order_numbers()
    {
        var names = new List<string>
        {
            "Forretning: IMUSIC.DK 11338750",
            "Forretning: IMUSIC.DK 10915939",
            "Forretning: IMUSIC.DK 10819093",
            "Forretning: IMUSIC.DK 10536514",
        };

        var groups = CategoryDuplicateFinder.FindGroups(names);

        groups.Should().ContainSingle();
        groups[0].Names.Should().BeEquivalentTo(names);
    }

    [Test]
    public void Different_named_people_never_get_grouped_even_with_a_shared_surname_fragment()
    {
        var names = new List<string> { "MobilePay Fie Tandrup Rokkjær", "MobilePay Anne-Christine Tandrup" };

        CategoryDuplicateFinder.FindGroups(names).Should().BeEmpty();
    }

    [Test]
    public void A_singleton_category_with_no_match_is_never_reported_as_a_group()
    {
        var names = new List<string> { "Husleje", "Forsikring", "Dagligvarer" };

        CategoryDuplicateFinder.FindGroups(names).Should().BeEmpty();
    }

    [TestCase("Rema 1000")]
    [TestCase("Debetkort    REMA1000 Aarhus, N")]
    public void Rema_1000_variants_group_together(string variant)
    {
        var names = new List<string> { "Rema 1000", "Debetkort    REMA1000 Aarhus, N", variant };

        var groups = CategoryDuplicateFinder.FindGroups(names);

        groups.Should().NotBeEmpty();
        groups.SelectMany(g => g.Names).Should().Contain(variant);
    }

    [Test]
    public void Telenor_variants_group_together()
    {
        var names = new List<string> { "telenor", "Debetkort    telenor.dk", "BS TELENOR" };

        var groups = CategoryDuplicateFinder.FindGroups(names);

        groups.Should().ContainSingle();
        groups[0].Names.Should().HaveCount(3);
    }
}
