using FluentAssertions;
using ITMartinLager.Server.Services;

namespace ITMartinLager.Tests;

public class DbaListingsTests
{
    // Trimmed copy of DBA's search page JSON-LD for "rifbjerg amagerdigte" (2026-10-07).
    private const string Page = """
        <html><script type="application/ld+json">{"@type":"BreadcrumbList","itemListElement":[]}</script>
        <script type="application/ld+json">{"@type":"CollectionPage","mainEntity":{"@type":"ItemList","itemListElement":[
          {"@type":"ListItem","position":1,"item":{"@type":"Product","name":"Amagerdigte (6. oplag, 1967), Klaus Rifbjerg","offers":{"price":"59","priceCurrency":"DKK"}}},
          {"@type":"ListItem","position":2,"item":{"@type":"Product","name":"Amagerdigte (1. oplag, 1965), Klaus Rifbjerg","offers":{"price":"250","priceCurrency":"DKK"}}},
          {"@type":"ListItem","position":3,"item":{"@type":"Product","name":"Amager Digte af Klaus Rifbjerg","offers":{"price":"100","priceCurrency":"DKK"}}},
          {"@type":"ListItem","position":4,"item":{"@type":"Product","name":"Rifbjergs digte, Klaus Rifbjerg","offers":{"price":"75","priceCurrency":"DKK"}}},
          {"@type":"ListItem","position":5,"item":{"@type":"Product","name":"Voliere (1962), Klaus Rifbjerg","offers":{"price":"225","priceCurrency":"DKK"}}}
        ]}}</script></html>
        """;

    [Test]
    public void Only_listings_of_the_same_title_count_and_give_the_price_range()
    {
        var l = DbaListings.Parse(Page, "Amagerdigte", "u");
        l.Count.Should().Be(3);
        (l.Low, l.Median, l.High).Should().Be((59m, 100m, 250m));
        l.Examples.Should().HaveCount(3);
    }

    [Test]
    public void No_matching_listing_is_zero_with_no_prices()
    {
        var l = DbaListings.Parse(Page, "Den kroniske uskyld", "u");
        l.Count.Should().Be(0);
        l.Low.Should().BeNull();
    }

    [Test]
    public void Broken_or_empty_page_is_zero_not_an_error() =>
        DbaListings.Parse("<html><script type=\"application/ld+json\">{oops</script></html>", "Amagerdigte", "u").Count.Should().Be(0);

    [Test]
    public void Query_is_the_authors_surname_and_the_title() =>
        DbaListings.QueryFor("Amagerdigte", "Klaus Rifbjerg").Should().Be("Rifbjerg Amagerdigte");
}
