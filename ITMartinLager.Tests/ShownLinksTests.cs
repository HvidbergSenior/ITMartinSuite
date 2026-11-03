using FluentAssertions;
using ITMartinLager.Domain;
using NUnit.Framework;

namespace ITMartinLager.Tests;

// User 2026-11-04: "links to too high prices like Ebay should just be hided".
public class ShownLinksTests
{
    private static PriceFind F(string source, int kr) => new() { Source = source, Kr = kr, Url = $"https://{source}.example/{kr}" };

    [Test]
    public void Buyers_see_no_eBay_Discogs_or_far_too_dear_listings()
    {
        var finds = new[] { F("DBA", 40), F("DBA", 50), F("Faraos", 45), F("DBA", 400), F("eBay", 30), F("Discogs", 35) };
        Shop.Shown(finds).Select(f => (f.Source, f.Kr)).Should().BeEquivalentTo(new[] { ("DBA", 40), ("DBA", 50), ("Faraos", 45) });
        Shop.Reference(finds)!.Source.Should().NotBe("eBay");
        Shop.Proof(finds).Should().NotContain(f => f.Kr == 400 || f.Source == "eBay" || f.Source == "Discogs");
    }
}
