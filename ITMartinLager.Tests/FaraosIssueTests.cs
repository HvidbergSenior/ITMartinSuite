using FluentAssertions;
using ITMartinLager.Infrastructure.PriceSites;
using NUnit.Framework;

namespace ITMartinLager.Tests;

// 2026-11-25 (user: "the app does not find the price but the url on faraos antikvarisk finds it"): Anders And & Co. 1969 nr. 19
// has a page with a price per condition but no copy in stock, and the search only lists copies in stock.
public sealed class FaraosIssueTests
{
    [TestCase("Anders And & Co. 19 1969", 1969, 19)]
    [TestCase("Anders And & Co. 1969 Nr. 19", 1969, 19)]
    [TestCase("Walt Disney's Anders And & Co. 3 1955", 1955, 3)]
    public void The_hunters_query_is_read_as_year_and_number(string query, int year, int nr) =>
        FaraosListings.IssueQuery(query).Should().Be((year, nr));

    [TestCase("Anders And & Co.")]
    [TestCase("Anders And & Co. Jumbobog 5")]
    [TestCase("Tintin 3 1969")]
    public void Other_queries_are_left_to_the_search(string query) => FaraosListings.IssueQuery(query).Should().BeNull();

    [Test]
    public void An_issue_page_gives_one_price_per_condition()
    {
        const string json = """
        {"items":[{"displayName":"Anders And & Co. 1969 Nr. 19","itemPath":"anders-and-co.-1969-nr.-19","menuPath":"/antikvarisk/disney/andersandco/andersandco1969/andersandco1969",
          "usedComicEditions":[{"editionName":"1. udgave, 1. oplag","conditions":[{"name":"Good","price":{"price":3.0}},{"name":"Fine","price":{"price":18.0}}]}]}]}
        """;
        var r = FaraosListings.Parse(json, "Anders And & Co. 19 1969", "", "https://www.faraos.dk/antikvarisk");
        r.Count.Should().Be(2);
        r.Proof.Select(o => o.Price).Should().BeEquivalentTo(new decimal?[] { 3, 18 });
        r.Proof.Should().OnlyContain(o => o.Url == "https://www.faraos.dk/antikvarisk/disney/andersandco/andersandco1969/andersandco1969/anders-and-co.-1969-nr.-19");
    }
}
