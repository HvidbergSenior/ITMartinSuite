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
}
