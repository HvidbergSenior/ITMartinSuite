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
    [TestCase(Kinds.AndersAnd, ProductTypes.Serieblade)]
    [TestCase(Kinds.Jumbobog, ProductTypes.Jumbo)]
    [TestCase(Kinds.Magasin, ProductTypes.Andet)]
    [TestCase(Kinds.Tegneserie, ProductTypes.Tegneserier)]
    [TestCase(Kinds.Bog, ProductTypes.Boeger)]
    [TestCase(Kinds.Magic, ProductTypes.Magic)]
    [TestCase(Kinds.Konsolspil, ProductTypes.Computerspil)]
    [TestCase(Kinds.Braetspil, ProductTypes.Braetspil)]
    [TestCase(Kinds.Kortspil, ProductTypes.Braetspil)]
    public void Every_kind_belongs_to_one_product_type(string kind, string type) => ProductTypes.For(kind).Should().Be(type);

    [TestCase("Criminal Minds – The First Season", true)]
    [TestCase("CSI: Miami – Hele første sæson", true)]
    [TestCase("Alias – The Complete Second Series", true)]
    [TestCase("Matador – Matador Kollektionen 1978-1982", true)]
    [TestCase("The Chris Rock Show – Seasons 1&2", true)]
    [TestCase("Heat", false)]
    public void A_season_box_is_found_by_its_title(string title, bool season) => Films.IsSeason(Kinds.Dvd, title).Should().Be(season);

    [Test]
    public void A_season_box_gets_the_season_word_in_its_search()
    {
        var i = new Item { Kind = Kinds.Dvd, Title = "Lost – The Complete Fourth Season" };
        i.Touch();
        i.Search.Should().EndWith(" " + Films.SeasonWord);
        var book = new Item { Kind = Kinds.Bog, Title = "Sæson for mord" };
        book.Touch();
        book.Search.Should().NotContain(Films.SeasonWord);
    }

    [Test]
    public void An_Anders_And_and_Co_read_as_a_comic_is_an_Anders_And_blad()
    {
        Kinds.Normalise("Tegneserie", "Anders And & Co.").Should().Be(Kinds.AndersAnd);
        Kinds.Normalise("tegneserie", " anders and ekstra ").Should().Be(Kinds.AndersAnd);
        Kinds.Normalise("Tegneserie", "Barks Klassiker").Should().Be(Kinds.Tegneserie);
        Kinds.Normalise("Jumbobog", "Anders And & Co.").Should().Be(Kinds.Jumbobog);
        Kinds.Normalise("Tegneserie", "Walt Disney's").Should().Be(Kinds.AndersAnd);
        Kinds.Normalise("Tegneserie", "Mammutbog").Should().Be(Kinds.Jumbobog);
        Kinds.Normalise("Tegneserie", "Jonah Hex").Should().Be(Kinds.Superhelte, "a blad - shown as Serieblade");
        Kinds.Normalise("Tegneserie", "Kong Kylie").Should().Be(Kinds.Superhelte);
        Kinds.Normalise("Tegneserie", "Agent 327").Should().Be(Kinds.Tegneserie, "an album stays");
        ProductTypes.For(Kinds.Superhelte, "Jonah Hex").Should().Be(ProductTypes.Serieblade);
        ProductTypes.For(Kinds.Superhelte, "Edderkoppen").Should().Be(ProductTypes.Superhelte);
        Kinds.Label(Kinds.Superhelte).Should().Be("Blad");
    }

    // User 2026-11-03: "Serieblade is Anders And, basserne, jonah hex, kong kylie. Superhelte is Project X, Edderkoppen",
    // "before 1990 all blade" = Gamle blade.
    [TestCase(Kinds.AndersAnd, "Anders And & Co.", 1995, ProductTypes.Serieblade)]
    [TestCase(Kinds.AndersAnd, "Anders And & Co.", 1968, ProductTypes.GamleBlade)]
    [TestCase(Kinds.AndersAnd, "Anders And & Co.", null, ProductTypes.Serieblade)]
    [TestCase(Kinds.Superhelte, "Kong Kylie", 1979, ProductTypes.GamleBlade)]
    [TestCase(Kinds.Superhelte, "Basserne", 1992, ProductTypes.Serieblade)]
    [TestCase(Kinds.Superhelte, "Project X", 2001, ProductTypes.Superhelte)]
    [TestCase(Kinds.Superhelte, "Edderkoppen", 1985, ProductTypes.GamleBlade)]
    [TestCase(Kinds.Tegneserie, "Lucky Luke", 1975, ProductTypes.Tegneserier)]
    [TestCase(Kinds.Jumbobog, "", 1980, ProductTypes.Jumbo)]
    public void Blade_go_by_series_and_year(string kind, string series, int? year, string type) =>
        ProductTypes.For(kind, series, year).Should().Be(type);

    // bogshoppen.dk shows five shelves (user 2026-11-03: "the search engine is too complex").
    [Test]
    public void Every_category_but_Andet_is_on_one_shelf()
    {
        foreach (var t in ProductTypes.All.Where(t => t != ProductTypes.Andet))
            ShopShelves.All.Count(s => s.Types.Contains(t)).Should().Be(1, t);
        ShopShelves.For(ProductTypes.GamleBlade).Should().Be(ShopShelves.Tegneserier);
        ShopShelves.For(ProductTypes.Andet).Should().Be("");
        ShopShelves.TypesOf([ShopShelves.Boeger, ShopShelves.Film]).Should().Equal(ProductTypes.Boeger, ProductTypes.Film);
    }

    [Test]
    public void Responsible_per_category()
    {
        ProductTypes.Responsible(ProductTypes.Film).Should().Be("Rico");
        ProductTypes.Responsible(ProductTypes.Tegneserier).Should().Be("Rico");
        ProductTypes.Responsible(ProductTypes.Serieblade).Should().Be("Martin");
        ProductTypes.Responsible(ProductTypes.GamleBlade).Should().Be("Martin");
    }

    [TestCase("Designing the User Interface", "", Languages.Engelsk)]
    [TestCase("Farewell, Moonshadow", "", Languages.Dansk)]
    [TestCase("Lawrence of Arabia", "", Languages.Engelsk)]
    [TestCase("Se mig dog! og andre historier", "", Languages.Dansk)]
    [TestCase("Kong Arthur – Director's Cut", "", Languages.Dansk)]
    [TestCase("Anders And & Co.", "Anders And & Co.", Languages.Dansk)]
    [TestCase("Stage Lighting Design", "", Languages.Dansk)]
    public void Language_is_guessed_from_the_title(string title, string series, string language) =>
        Languages.Guess(title, series).Should().Be(language);

    [Test]
    public void Touch_sorts_an_item_and_keeps_a_read_language()
    {
        var i = new Item { Kind = Kinds.AndersAnd, Series = "Anders And & Co.", Year = 1971, Title = "Anders And" };
        i.Touch();
        (i.Category, i.Language).Should().Be((ProductTypes.GamleBlade, Languages.Dansk));
        var b = new Item { Kind = Kinds.Bog, Title = "Stage Lighting Design", Language = Languages.Engelsk };
        b.Touch();
        b.Language.Should().Be(Languages.Engelsk, "the reader said so - the guess never overrides it");
        b.Classify().Should().BeFalse();
    }

    [Test]
    public void Every_product_type_with_a_service_has_a_gold_list() =>
        // Gamle blade share the blade lists; Computerspil and Brætspil og kortspil have none yet (2026-11-03).
        Gold.Types.Should().BeEquivalentTo(ProductTypes.All.Except([ProductTypes.Andet, ProductTypes.GamleBlade, ProductTypes.Computerspil, ProductTypes.Braetspil]));

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
