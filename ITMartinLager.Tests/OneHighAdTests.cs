using FluentAssertions;
using ITMartinLager.Domain;

namespace ITMartinLager.Tests;

// "Én høj annonce" (user 2026-11-21: "a category for me to get all products where ONE ad shows high price").
public class OneHighAdTests
{
    private static PriceFind F(string source, int kr, bool similar = false, bool isNew = false) =>
        new() { Source = source, Kr = kr, Similar = similar, IsNew = isNew, Name = "x", Url = "https://x" };

    private static Item Blad(decimal hint) => new() { Kind = Kinds.Superhelte, Title = "Dæmonen", PriceHint = hint };

    [Test]
    public void One_dealer_listing_far_over_the_standard_is_flagged()
    {
        // Dæmonen live 21.11: Faraos 425 kr alone (DBA 40 never counts).
        Pricing.OneHighAd(Blad(400), [F("Faraos", 425), F("DBA", 40), F("DBA", 40)]).Should().BeTrue();
    }

    [Test]
    public void Two_counted_listings_or_a_low_one_is_not()
    {
        Pricing.OneHighAd(Blad(300), [F("Faraos", 360), F("Nemos Bibliotek", 300)]).Should().BeFalse();
        Pricing.OneHighAd(Blad(30), [F("Faraos", 50)]).Should().BeFalse();   // standard 40 -> needs 80+
    }

    [Test]
    public void Own_price_rare_and_similar_only_are_left_out()
    {
        var priced = Blad(400); priced.Price = 200;
        Pricing.OneHighAd(priced, [F("Faraos", 425)]).Should().BeFalse();
        var rare = Blad(400); rare.Rare = true;
        Pricing.OneHighAd(rare, [F("Faraos", 425)]).Should().BeFalse();
        Pricing.OneHighAd(Blad(400), [F("Faraos", 425, similar: true)]).Should().BeFalse();
    }
}

// Bog & idé as a reference, new or used (2026-11-21).
public class NewReferenceTests
{
    [Test]
    public void The_cheapest_new_copy_at_a_Danish_new_shop()
    {
        var finds = new List<PriceFind>
        {
            new() { Source = "Bog & idé", Kr = 249, IsNew = true, Url = "https://bogide.dk/2" },
            new() { Source = "Bog & idé", Kr = 199, IsNew = true, Url = "https://bogide.dk/1" },
            new() { Source = "Faraos", Kr = 60, Url = "https://faraos.dk/1" },
            new() { Source = "Saxo", Kr = 150, IsNew = true, Url = "https://saxo.dk/1" },
        };
        Shop.NewReference(finds)!.Url.Should().Be("https://bogide.dk/1");
        Shop.NewReference([finds[2]]).Should().BeNull();
    }
}

// Seasons and box sets priced from the ads at half; a single DVD keeps the standard price (2026-11-21).
public class FilmBoxTests
{
    private static PriceFind Ad(int kr) => new() { Source = "DBA", Kr = kr, Name = "x", Url = $"https://dba.dk/{kr}" };

    [Test]
    public void A_season_gets_half_the_ads_on_a_step_and_the_ad_as_reference()
    {
        var season = new Item { Kind = Kinds.Dvd, Title = "Matador Kollektionen" };
        Pricing.FromAds(season, [Ad(200), Ad(240), Ad(260)]).Should().Be(100);   // middle 240 -> 120 -> step 100
        Shop.Reference([Ad(200), Ad(240)], 100, ads: true)!.Url.Should().Be("https://dba.dk/200");
    }

    [Test]
    public void A_single_dvd_and_cheap_ads_stay_on_the_standard()
    {
        Pricing.FromAds(new Item { Kind = Kinds.Dvd, Title = "Gladiator" }, [Ad(200)]).Should().BeNull();
        Pricing.FromAds(new Item { Kind = Kinds.Dvd, Title = "Bones sæson 2" }, [Ad(20)]).Should().Be(20);   // never under standard 20
    }
}

public class GamePriceTests
{
    [Test]
    public void A_console_game_is_priced_from_the_ads_at_half()
    {
        var game = new Item { Kind = Kinds.Konsolspil, Title = "The Last of Us", Platform = "PlayStation 3" };
        Pricing.FromAds(game, [new PriceFind { Source = "DBA", Kr = 100, Name = "x", Url = "https://dba.dk/1" }]).Should().Be(50);
    }
}

// Book sub-genres from the same Open Library subjects (2026-11-21).
public class BookSubGenreTests
{
    [Test]
    public void Spy_stories_are_a_spy_thriller_and_scandinavian_crime_is_nordic()
    {
        GenreRules.BookSubGenre("Thriller", ["Fiction", "Spy stories", "Espionage"]).Should().Be("Spionthriller");
        GenreRules.BookSubGenre("Krimi", ["Detective and mystery stories", "Sweden", "Fiction"]).Should().Be("Nordisk krimi");
        GenreRules.BookSubGenre("Fagbøger", ["Law"]).Should().BeEmpty();
    }
}
