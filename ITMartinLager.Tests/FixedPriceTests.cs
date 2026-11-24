using FluentAssertions;
using ITMartinLager.Domain;

namespace ITMartinLager.Tests;

// Fixed price per kind (user 2026-11-24): standard price, unless the typical used price found is >= 2x standard and >= 50 kr -
// then Martin sets it. DBA only counts when no other source found the thing.
public class FixedPriceTests
{
    private static Item Dvd(decimal? price = null) => new() { Kind = Kinds.Dvd, Category = ProductTypes.Film, Price = price, PricesAt = DateTime.UtcNow, Confidence = 1 };
    private static PriceFind F(string source, int kr, bool isNew = false, bool similar = false) => new() { Source = source, Kr = kr, IsNew = isNew, Similar = similar };

    [Test]
    public void Ordinary_thing_gets_no_hint_so_the_fixed_price_shows() =>
        FixedPrice.Hint(Dvd(), [F("Faraos", 30), F("DBA", 200)], 120m).Should().BeNull("Faraos says 30 kr - DBA is ignored when another source found it");

    [Test]
    public void Clearly_higher_needs_martins_price()
    {
        var finds = new[] { F("Faraos", 90), F("Nemos Bibliotek", 110) };
        FixedPrice.NeedsOwnPrice(Dvd(), finds).Should().BeTrue();
        FixedPrice.Hint(Dvd(), finds, null).Should().BeGreaterThanOrEqualTo(50, "a hint of 50+ makes the shop say 'vi vurderer prisen'");
    }

    [Test]
    public void Dba_counts_only_when_nothing_else_was_found()
    {
        FixedPrice.Typical([F("DBA", 100), F("DBA", 120)]).Should().Be(110);
        FixedPrice.Typical([F("DBA", 100), F("Faraos", 25)]).Should().Be(25);
    }

    [Test]
    public void Twice_the_standard_but_under_50_kr_keeps_the_fixed_price() =>
        FixedPrice.NeedsOwnPrice(Dvd(), [F("Faraos", 45)]).Should().BeFalse("20 kr standard, 45 kr is over 2x but under 50 kr");

    [Test]
    public void Own_price_and_rare_things_are_left_alone()
    {
        FixedPrice.NeedsOwnPrice(Dvd(price: 30), [F("Faraos", 300)]).Should().BeFalse();
        var rare = Dvd(); rare.Rare = true;
        FixedPrice.Hint(rare, [F("Faraos", 300)], 250m).Should().Be(250m);
    }

    [Test]
    public void New_and_similar_listings_do_not_count() =>
        FixedPrice.Typical([F("Faraos", 300, isNew: true), F("Faraos", 300, similar: true)]).Should().BeNull();

    [Test]
    public void A_blurry_magazine_keeps_the_fixed_price()
    {
        var blad = new Item { Kind = Kinds.AndersAnd, Category = ProductTypes.Serieblade, PricesAt = DateTime.UtcNow, Confidence = 1, ItemPhoto = "x.jpg", PhotoHidden = true };
        FixedPrice.NeedsOwnPrice(blad, [F("Faraos", 300)]).Should().BeFalse("a hidden (blurry) blad photo cannot prove the issue");
        blad.PhotoHidden = false;
        FixedPrice.NeedsOwnPrice(blad, [F("Faraos", 300), F("Nemos Bibliotek", 280)]).Should().BeTrue();
    }

    [Test]
    public void One_listing_is_not_enough() =>
        FixedPrice.NeedsOwnPrice(Dvd(), [F("Faraos", 300)]).Should().BeFalse("one listing is often another product");

    [Test]
    public void An_unsure_reading_never_goes_on_the_list()
    {
        // "First Wave" (40 % sure): three DBA listings for a doll, a card game and a history book gave 1000 kr (2026-11-24).
        var unsure = Dvd(); unsure.Confidence = 0.4;
        FixedPrice.NeedsOwnPrice(unsure, [F("DBA", 329), F("DBA", 1000), F("DBA", 3300)]).Should().BeFalse();
        FixedPrice.Hint(unsure, [F("DBA", 329), F("DBA", 1000), F("DBA", 3300)], 1000m).Should().BeNull();
    }

    [Test]
    public void A_listing_for_a_special_version_does_not_count()
    {
        // Anders And 17/1973: both listings were "trykvariant (uden dato)" - ours has the date (2026-11-24).
        var aa = new Item { Kind = Kinds.AndersAnd, Category = ProductTypes.Serieblade, PricesAt = DateTime.UtcNow, Confidence = 1, ItemPhoto = "x.jpg" };
        var finds = new[] { new PriceFind { Source = "DBA", Kr = 150, Name = "Anders And & Co nr. 17 (1973) – SJÆLDEN TRYKVARIANT UDEN DATO" },
                            new PriceFind { Source = "DBA", Kr = 1500, Name = "Anders And & Co nr. 17 – 1973 – Trykvariant – flot samler-eksemplar" } };
        FixedPrice.NeedsOwnPrice(aa, finds).Should().BeFalse();
        aa.Note = "Trykvariant uden dato";
        FixedPrice.NeedsOwnPrice(aa, finds).Should().BeTrue("our own copy IS the variant");
    }

    [Test]
    public void A_gold_entry_for_a_special_version_only_matches_a_special_thing()
    {
        var gold = new List<GoldEntry> { new() { Category = "Anders And & Co.", Kind = Kinds.AndersAnd, Number = "17", Year = 1973, Kr = 1500,
            Name = "Anders And & Co nr. 17 (25. årgang) – 1973 – Trykvariant – flot samler-eksemplar" } };
        Gold.Match(gold, Kinds.AndersAnd, "Anders And & Co.", "Anders And & Co.", "17", 1973, "Anders And & Co.").Should().BeNull();
        Gold.Match(gold, Kinds.AndersAnd, "Anders And & Co.", "Anders And & Co.", "17", 1973, "Anders And & Co. Trykvariant uden dato").Should().NotBeNull();
    }

    [Test]
    public void Our_price_is_under_the_market_and_under_the_new_price()
    {
        // Caza "Fantasier fra forstæderne": Nemos 375 + Faraos 130 (typical 252), Bog & idé new 300 (2026-11-24).
        var bog = new Item { Kind = Kinds.Bog, Category = ProductTypes.Boeger, PricesAt = DateTime.UtcNow, Confidence = 1, ItemPhoto = "x.jpg" };
        var hint = FixedPrice.Hint(bog, [F("Nemos Bibliotek", 375), F("Faraos", 130), F("Bog & idé", 300, isNew: true)], null);
        hint.Should().BeLessThanOrEqualTo(180, "at most 60 % of the new price");
        FixedPrice.OurPrice(252, null).Should().BeLessThan(252, "10-25 % under the market");
    }

    [Test]
    public void A_box_set_follows_the_same_rules()
    {
        // In Treatment Season Three: ONE DBA listing "sæson 1 og 2" at 400 kr gave 200 kr (2026-11-24).
        var box = new Item { Kind = Kinds.Dvd, Category = ProductTypes.Film, Title = "In Treatment – Season Three", PricesAt = DateTime.UtcNow, Confidence = 1, ItemPhoto = "x.jpg" };
        FixedPrice.Applies(box).Should().BeTrue();
        FixedPrice.Hint(box, [new PriceFind { Source = "DBA", Kr = 400, Name = "DVD-bokse In Treatment sæson 1 og 2" }], 200m).Should().BeNull();
    }

    [Test]
    public void A_listing_for_another_year_of_a_numbered_issue_does_not_count()
    {
        // Skipper Skræk nr. 26 from 1960: Faraos had nr. 26 from 1958 and 1962 (2026-11-24).
        var blad = new Item { Kind = Kinds.Tegneserie, Category = ProductTypes.Serieblade, Number = "26", Year = 1960, PricesAt = DateTime.UtcNow, Confidence = 1, ItemPhoto = "x.jpg" };
        FixedPrice.OtherIssue(blad, "Skipper Skræk 1962 Nr. 26").Should().BeTrue();
        FixedPrice.OtherIssue(blad, "Skipper Skræk 1960 Nr. 26").Should().BeFalse();
        FixedPrice.Chosen([new PriceFind { Source = "Faraos", Kr = 12, Name = "Skipper Skræk 1962 Nr. 26" }, new PriceFind { Source = "Faraos", Kr = 10, Name = "Skipper Skræk 1958 Nr. 26" }], blad).Should().BeEmpty();
    }
}
