using FluentAssertions;
using ITMartinLager.Domain;
using ITMartinLager.Infrastructure.PriceSites;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace ITMartinLager.Tests;

// 2026-11-16 "byg bedre prissøgning": DBA and GulogGratis search only the category the thing is sold in, so
// "E-Man" no longer finds MAN trucks, "Vox" amplifiers or "X-Force" a Kawasaki.
public class MarketCategoryTests
{
    [Test]
    public void A_comic_on_DBA_is_searched_among_books_and_collectibles() =>
        DbaListings.SearchUrl("E-Man 11", Kinds.Superhelte).Should()
            .Be("https://www.dba.dk/recommerce/forsale/search?q=E-Man%2011&sub_category=1.86.5209&sub_category=1.86.285");

    [TestCase("CD", "&sub_category=1.86.3922")]
    [TestCase("DVD", "&sub_category=1.86.3922")]
    [TestCase("Konsolspil", "&sub_category=1.93.3905")]
    [TestCase("Brætspil", "&sub_category=1.86.5203&sub_category=1.86.285")]
    [TestCase("Merchandise", "")]
    [TestCase("", "")]
    public void Each_kind_has_its_DBA_category(string kind, string query) => MarketCategories.DbaQuery(kind).Should().Be(query);

    [TestCase("Anders And-blad", "https://www.guloggratis.dk/kategori/underholdning-og-laering/q-anders-1969")]
    [TestCase("Vinyl", "https://www.guloggratis.dk/kategori/underholdning-og-laering/musik/q-anders-1969")]
    [TestCase("Andet", "https://www.guloggratis.dk/kategori/q-anders-1969")]
    public void GulogGratis_searches_the_kinds_category(string kind, string url)
    {
        var gg = new GulogGratisListings(new HttpClient(), Microsoft.Extensions.Logging.Abstractions.NullLogger<GulogGratisListings>.Instance);
        gg.UrlFor("Anders 1969", kind).Should().Be(url);
    }

    // The title match keeps hyphenated names whole and short words whole (2026-11-16 junk: Emanuele coins for E-Man,
    // an Air Force guide for X-Force).
    [TestCase("E-Man 11", "Italien, Kongerig Vittorio Emanuele III. Lotto 11 monete", false)]
    [TestCase("E-Man 11", "Manhattan nocturne af Colin Harrison (f. 1960-11-27)", false)]
    [TestCase("E-Man 11", "E-Man nr. 11 Charlton 1975", true)]
    [TestCase("X-Force 1", "BOG: GUIDE FOR AIR FORCE WRITING, 1. NOVEMBER 1973", false)]
    [TestCase("X-Force 1", "X-Force #1 Marvel 1991", true)]
    [TestCase("Spider-Man 5", "Spider Man nr 5", true)]
    [TestCase("Spider Man 5", "Spiderman 5", true)]
    [TestCase("Amager digte", "Amagerdigte, Klaus Rifbjerg", true)]
    public void The_title_match(string title, string listing, bool same) =>
        ListingParse.Matcher(title, "").Matches(listing).Should().Be(same);

    // Every price-hunted kind has a category on both sites - but Merchandise and Andet, which can be anything.
    [Test]
    public void Every_hunted_kind_has_a_category() =>
        Pricing.HuntedKinds.Except([Kinds.Merch, Kinds.Andet])
            .Where(k => MarketCategories.Dba(k).Length == 0 || MarketCategories.GulogGratis(k) == "")
            .Should().BeEmpty();
}

// The titles that gave junk prices 2026-11-16 (#1062 E-Man 11500 kr, #3372 X-Force 13495, #3257 Vox 900 ...), asked live.
public class MarketCategoryLiveTests
{
    [Test, Explicit, Category("Live")]
    public async Task Live_the_junk_titles_now()
    {
        var services = new ServiceCollection().AddLogging();
        PriceSourceRegistration.AddPriceSources(services);
        var sources = services.BuildServiceProvider().GetServices<ITMartinLager.Application.IPriceSource>()
            .Where(s => s.Name is "DBA" or "GulogGratis").ToList();
        foreach (var title in new[] { "E-Man 11", "X-Force 1", "X-Men 83", "Vox", "Vigilante 1", "Zen" })
            foreach (var s in sources)
            {
                var r = await s.LookupAsync(title, "", "", Kinds.Superhelte, CancellationToken.None);
                TestContext.Out.WriteLine($"{title} | {s.Name} | {r?.All.Count} kept | median {r?.Median} | {s.SearchUrl(title, "", "", Kinds.Superhelte)}");
                foreach (var o in r?.All.Take(4) ?? []) TestContext.Out.WriteLine($"      {o.Price} | {o.Name}");
            }
    }
}
