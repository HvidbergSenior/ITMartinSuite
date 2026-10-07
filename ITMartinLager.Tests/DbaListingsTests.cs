using FluentAssertions;
using ITMartinLager.Server.Services;

namespace ITMartinLager.Tests;

public class DbaListingsTests
{
    // Trimmed copy of DBA's search page JSON-LD for "rifbjerg amagerdigte" (2026-10-07).
    private const string Page = """
        <html><script type="application/ld+json">{"@type":"BreadcrumbList","itemListElement":[{"@type":"ListItem","position":1,"name":"Amagerdigte","item":"https://www.dba.dk/"}]}</script>
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
        var l = ListingParse.Parse(Page, "Amagerdigte", "u", _ => 1m);
        l.Count.Should().Be(3);
        (l.Low, l.Median, l.High).Should().Be((59m, 100m, 250m));
        l.Examples.Should().HaveCount(3);
    }

    [Test]
    public void No_matching_listing_is_zero_with_no_prices()
    {
        var l = ListingParse.Parse(Page, "Den kroniske uskyld", "u", _ => 1m);
        l.Count.Should().Be(0);
        l.Low.Should().BeNull();
    }

    [Test]
    public void Broken_or_empty_page_is_zero_not_an_error() =>
        ListingParse.Parse("<html><script type=\"application/ld+json\">{oops</script></html>", "Amagerdigte", "u", _ => 1m).Count.Should().Be(0);

    [Test]
    public void Query_is_the_authors_surname_and_the_title() =>
        DbaListings.QueryFor("Amagerdigte", "Klaus Rifbjerg").Should().Be("Rifbjerg Amagerdigte");

    [Test]
    public void Prices_over_three_times_the_middle_are_shown_apart_as_unreasonable()
    {
        var l = ListingParse.Summarise(5, [400m, 600m, 630m, 990m, 6700m], "u", []);
        (l.Low, l.Median, l.High, l.Wild).Should().Be((400m, 630m, 990m, 6700m));
        l.Text.Should().Contain("urimelige internetpriser op til 6700 kr");
    }

    [Test]
    public void Usd_listings_are_converted_and_unknown_currencies_skipped()
    {
        const string abe = """
            <script type="application/ld+json">{"@type":"ItemList","itemListElement":[
            {"@type":"ListItem","item":{"@type":"Book","name":"Gotrek & Felix: The Third Omnibus","offers":{"price":89.95,"priceCurrency":"USD"}}},
            {"@type":"ListItem","item":{"@type":"Book","name":"Gotrek & Felix : the Third Omnibus","offers":{"price":50,"priceCurrency":"GBP"}}}]}</script>
            """;
        var l = ListingParse.Parse(abe, "Gotrek & Felix: The Third Omnibus", "u", c => c == "USD" ? 6.633m : null);
        l.Count.Should().Be(2);
        (l.Low, l.High).Should().Be((597m, 597m));
    }

    [TestCase("1844167333", "9781844167333")]
    [TestCase("87-00-12345-X", "9788700123458")]
    [TestCase("978-1-84416-733-3", "9781844167333")]
    public void Isbn10_is_turned_into_isbn13(string typed, string isbn13) =>
        BarcodeLookup.ToIsbn13(typed).Should().Be(isbn13);
}
