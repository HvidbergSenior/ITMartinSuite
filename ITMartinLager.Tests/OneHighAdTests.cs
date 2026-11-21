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
