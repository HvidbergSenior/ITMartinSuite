using FluentAssertions;
using ITMartinLager.Domain;

namespace ITMartinLager.Tests;

// The product types and bronze, silver, gold for every one of them (user 2026-10-20: "I want bronze, silver, gold on
// ever producttype").
public class ProductTypeGoldTests
{
    private static Offer O(string name, decimal kr) => new(name, kr, "https://dba.dk/" + kr);
    private static GoldCategory Cat(string name) => Gold.Categories.First(c => c.Name == name);

    [TestCase(Kinds.Vhs, ProductTypes.Film)]
    [TestCase(Kinds.BluRay, ProductTypes.Film)]
    [TestCase(Kinds.Vinyl, ProductTypes.Musik)]
    [TestCase(Kinds.Kassette, ProductTypes.Musik)]
    [TestCase(Kinds.Superhelte, ProductTypes.Superhelte)]
    [TestCase(Kinds.AndersAnd, ProductTypes.Blade)]
    [TestCase(Kinds.Tegneserie, ProductTypes.Tegneserier)]
    [TestCase(Kinds.Bog, ProductTypes.Boeger)]
    [TestCase(Kinds.Magic, ProductTypes.Magic)]
    [TestCase(Kinds.Konsolspil, ProductTypes.Andet)]
    public void Every_kind_belongs_to_one_product_type(string kind, string type) => ProductTypes.For(kind).Should().Be(type);

    [Test]
    public void Every_product_type_with_a_service_has_a_gold_list() =>
        Gold.Types.Should().BeEquivalentTo(ProductTypes.All.Where(t => t != ProductTypes.Andet));

    [Test]
    public void A_record_is_gold_by_its_title_and_the_record_player_is_not_counted()
    {
        var picked = Gold.Pick(Cat("Vinyl"),
        [
            O("Pink Floyd The Dark Side of the Moon LP 1. press Harvest", 900),
            O("Pink Floyd The Dark Side of the Moon LP 1. press Harvest", 700),
            O("Bang & Olufsen pladespiller Beogram 4000", 3000),
            O("Samling 200 LP plader", 2500),
        ], "DBA", DateTime.UtcNow);

        picked.Should().ContainSingle().Which.Should().Match<GoldEntry>(e => e.Kr == 900 && e.Listings == 2 && e.Badge == "🏆 GULD");
        Gold.Match(picked, Kinds.Vinyl, "The Dark Side of the Moon", "", "", null).Should().NotBeNull();
        Gold.Match(picked, Kinds.Vinyl, "Wish You Were Here", "", "", null).Should().BeNull();
        Gold.Match(picked, Kinds.Cd, "The Dark Side of the Moon", "", "", null).Should().BeNull();   // a CD is not the LP
    }

    [Test]
    public void One_short_word_is_too_little_to_call_a_title_gold()
    {
        var picked = Gold.Pick(Cat("VHS"), [O("Heat VHS 1995 sjælden dansk udgave", 300)], "DBA", DateTime.UtcNow);
        Gold.Match(picked, Kinds.Vhs, "Heat", "", "", null).Should().BeNull();
    }

    [Test]
    public void Superhero_issues_need_a_number_and_get_the_badge_for_theirs_only()
    {
        var picked = Gold.Pick(Cat("X-Men"), [O("X-Men nr. 1 1963 Marvel", 2000), O("X-Men tegneserie", 400)], "DBA", DateTime.UtcNow);
        picked.Should().ContainSingle().Which.Number.Should().Be("1");
        Gold.Match(picked, Kinds.Superhelte, "X-Men", "", "1", 1963).Should().NotBeNull();
        Gold.Match(picked, Kinds.Superhelte, "X-Men", "", "2", 1963).Should().BeNull();
    }

    [Test]
    public void Magic_counts_single_cards_not_sealed_boxes()
    {
        var picked = Gold.Pick(Cat("Magic-kort"), [O("Black Lotus Unlimited MTG", 90000), O("Dominaria booster display", 1200)], "DBA", DateTime.UtcNow);
        picked.Should().ContainSingle().Which.Name.Should().StartWith("Black Lotus");
        Gold.Interesting(picked, Kinds.Magic, "Black Lotus", "", "", null).Should().StartWith("🏆 GULD");
    }
}
