using FluentAssertions;
using ITMartinLager.Infrastructure.PriceSites;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ITMartinLager.Tests;

// 2026-10-25: Accord and Fantask. The parse tests use trimmed real answers; the live ones ask the real shops
// (run by hand: dotnet test --filter TestCategory=Live).
public class DanishShopTests
{
    private const string AccordJson = """
        {"resources":{"results":{"products":[
          {"title":"Robbie Williams - Escapology","price":"50.00","url":"/products/robbie-williams-escapology?_pos=1","tags":["condition=Brugt","genre=Rock/Pop","type=CD"]},
          {"title":"Robbie Williams - Escapology","price":"299.95","url":"/products/robbie-williams-escapology-lp","tags":["condition=Nyt","type=Vinyl"]},
          {"title":"Robbie Williams - Escapology","price":"125.00","url":"/products/x","tags":["condition=Brugt","condition=Nyt","type=CD"]},
          {"title":"Robbie Williams - Escapology","price":"200.00","url":"/products/y","tags":["condition=Brugt","type=Vinyl"]}
        ]}}}
        """;

    [Test]
    public void Accord_counts_only_used_copies_in_the_items_own_format()
    {
        var cd = AccordListings.Parse(AccordJson, "Escapology", "Robbie Williams", AccordListings.Cd, "u");
        cd.All.Should().ContainSingle().Which.Price.Should().Be(50);
        cd.All[0].Url.Should().Be("https://accord.dk/products/robbie-williams-escapology");
        AccordListings.Parse(AccordJson, "Escapology", "Robbie Williams", AccordListings.Vinyl, "u").All.Should().ContainSingle()
            .Which.Price.Should().Be(200);
    }

    [Test]
    public void Fantask_quick_search_gives_titles_and_item_pages()
    {
        const string html = """<a href="/detail.asp?category=comics&amp;itemid=185053">Amazing <mark>spider-man</mark>: Blood Hunt <span>(comics)</span></a><a href="/detail.asp?category=comics&amp;itemid=194886">Amazing <mark>spider-man</mark>: Torn <span>(comics)</span></a>""";
        FantaskListings.Hits(html).Should().BeEquivalentTo([
            ("/detail.asp?category=comics&itemid=185053", "Amazing spider-man: Blood Hunt"),
            ("/detail.asp?category=comics&itemid=194886", "Amazing spider-man: Torn")]);
    }

    private static HttpClient Http() => new(new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.All }) { Timeout = TimeSpan.FromSeconds(30) };

    [Test, Explicit, Category("Live")]
    public async Task Live_Accord()
    {
        var r = await new AccordListings(Http(), NullLogger<AccordListings>.Instance).LookupAsync("Escapology", "Robbie Williams", AccordListings.Cd);
        TestContext.Out.WriteLine(string.Join("\n", r!.All.Select(o => $"{o.Name} | {o.Price} | {o.Url}")));
        r.Should().NotBeNull();
    }

    [Test, Explicit, Category("Live")]
    public async Task Live_Fantask()
    {
        var r = await new FantaskListings(Http(), NullLogger<FantaskListings>.Instance).LookupAsync("Amazing Spider-Man Blood Hunt", "");
        TestContext.Out.WriteLine(string.Join("\n", r!.All.Select(o => $"{o.Name} | {o.Price} | {o.Url}")));
        r.All.Should().NotBeEmpty();
    }
}
