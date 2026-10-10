using FluentAssertions;
using ITMartinLager.Server.Data;
using ITMartinLager.Server.Services;

namespace ITMartinLager.Tests;

public class PriceHuntTests
{
    // Trimmed copy of one Bokbörsen search result for "pratchett mort" (2026-10-10).
    private const string Bokborsen = """
        <div class="single-product content item group">
          <h2><a href="/view/Terry-Pratchett/Mort/13741417"><span itemprop="name" data-expanded-value="Mort">Mort</span></a></h2>
          <span class="author" itemprop="author" itemscope itemtype="https://schema.org/Person">av
            <a href="https://www.bokborsen.se?f=1&amp;qa=Terry Pratchett" data-control-filter-author data-value="Terry Pratchett" itemprop="name"
               data-expanded-value="Terry Pratchett">Terry Pratchett</a></span>
          <button class="button buy add-to-cart" itemprop="price" content="170 SEK"><span class="price">170 SEK</span></button>
        </div>
        <div class="single-product content item group">
          <h2><a href="/view/Terry-Pratchett/Eric/1"><span itemprop="name" data-expanded-value="Eric">Eric</span></a></h2>
          <span class="author"><a itemprop="name"
               data-expanded-value="Terry Pratchett">Terry Pratchett</a></span>
          <button itemprop="price" content="90 SEK"></button>
        </div>
        """;

    [Test]
    public void Bokborsen_listing_gives_title_author_kr_original_and_link()
    {
        var l = BokborsenListings.Parse(Bokborsen, "Mort", "Terry Pratchett", "u", 0.65m);
        l.Count.Should().Be(1);
        var o = l.All.Single();
        o.Name.Should().Be("Mort – Terry Pratchett");
        o.Price.Should().Be(110m);   // 170 SEK * 0.65 = 110.5, rounded to even
        o.Original.Should().Be("170,00 SEK");
        o.Url.Should().Be("https://www.bokborsen.se/view/Terry-Pratchett/Mort/13741417");
    }

    [Test]
    public void Bokborsen_by_isbn_takes_every_hit()
    {
        BokborsenListings.Parse(Bokborsen, "", "", "u", 0.65m).Count.Should().Be(2);
    }

    // Trimmed Bog & idé (Shopify) search/suggest.json answer (2026-10-10). The name has no author, the address does.
    private const string BogIde = """
        {"resources":{"results":{"products":[
          {"title":"Mort","price":"129.95","url":"/products/mort-terry-pratchett-paperback-123?_pos=1"},
          {"title":"Mort","price":"99.95","url":"/products/mort-anne-hansen-haeftet-9?_pos=2"},
          {"title":"The Magic of Terry Pratchett","price":"189.95","url":"/products/the-magic-of-terry-pratchett-marc-burrows?_pos=3"}
        ]}}}
        """;

    [Test]
    public void Bog_ide_matches_the_author_in_the_product_address()
    {
        var l = BogIdeListings.Parse(BogIde, "Mort", "Terry Pratchett", "u");
        l.Count.Should().Be(1);
        l.All.Single().Price.Should().Be(130m);
        l.All.Single().Url.Should().Be("https://www.bog-ide.dk/products/mort-terry-pratchett-paperback-123");
    }

    [Test]
    public void Normal_price_leaves_out_the_dreamers()
    {
        PriceHunt.Summary.NormalOf([5, 5, 10, 10, 15, 400]).Should().Be(10);
        PriceHunt.Summary.NormalOf([]).Should().BeNull();
    }

    [Test]
    public void Far_off_prices_are_flagged_with_how_far()
    {
        PriceHunt.Summary.Flag(50, 5).Should().Be("10× normalprisen");
        PriceHunt.Summary.Flag(12, 10).Should().Be("");
        PriceHunt.Summary.Flag(3, 10).Should().Be("under en tredjedel af normalprisen");
        PriceHunt.Summary.Flag(50, null).Should().Be("");
    }

    [Test]
    public void Keeps_the_cheapest_the_middle_and_the_dearest()
    {
        var offers = Enumerable.Range(1, 30).Select(i => new Offer($"b{i}", i * 10m, $"u{i}")).ToList();
        var kept = PriceHunt.Keep(ListingParse.Summarise(offers, "u")).Select(o => (int)o.Price!.Value).ToList();
        kept.Should().HaveCount(12);
        kept.Should().Contain([10, 20, 290, 300]);
        kept.Should().Contain(160);
    }

    [Test]
    public void Comics_are_searched_with_their_number()
    {
        PriceHunt.TitleFor(new Item { Title = "", Series = "Anders And & Co.", Number = "12" }).Should().Be("Anders And & Co. 12");
        PriceHunt.TitleFor(new Item { Title = "Tintin 5", Number = "5" }).Should().Be("Tintin 5");
        PriceHunt.TitleFor(new Item { Title = "Mort" }).Should().Be("Mort");
    }

    [Test]
    public void A_one_word_title_is_never_part_of_another_word_or_another_author()
    {
        var m = ListingParse.Matcher("Mort", "Terry Pratchett");
        m.Matches("Blake og Mortimer 3 – Det gule mærke").Should().BeFalse();
        m.Matches("Mort Walker – Håndbog i tegneseriemageri").Should().BeFalse();
        m.Matches("Mort, Terry Pratchett").Should().BeTrue();
        ListingParse.Matcher("Mort", "").Matches("Mort: (Discworld Novel 4)").Should().BeTrue();
    }

    [Test]
    public void Placeholder_prices_are_not_prices()
    {
        ListingParse.Placeholder(11111111m).Should().BeTrue();
        ListingParse.Placeholder(9999m).Should().BeTrue();
        ListingParse.Placeholder(1000m).Should().BeFalse();
        ListingParse.Placeholder(55m).Should().BeFalse();
    }

    [Test]
    public void Zvab_and_AbeBooks_share_listing_numbers()
    {
        PriceHunt.ListingNumber("https://www.zvab.com/Mort-Terry-Pratchett-Corgi-Books/31992988332/bd").Should().Be("31992988332");
        PriceHunt.ListingNumber("https://www.abebooks.com/Mort-Terry-Pratchett-Corgi-Books/31992988332/bd").Should().Be("31992988332");
        PriceHunt.ListingNumber("https://www.dba.dk/recommerce/forsale/item/1945166").Should().Be("");
    }

    // Trimmed Kleinanzeigen search result (2026-10-10): the title sits in an image JSON block, the price as "5 € VB".
    private const string Kleinanzeigen = """
        <article class="flex justify-between p-medium" data-adid="1" data-href="/s-anzeige/die-farben-der-magie/1-76-1">
          <script type="application/ld+json">{"title":"Terry Pratchett – Die Farben der Magie, gebunden","@type":"ImageObject"}</script>
          <p class="price">  5 € VB </p></article>
        <article class="flex" data-adid="2" data-href="/s-anzeige/die-hexen/2">
          <script type="application/ld+json">{"title":"Terry Pratchett Die Hexen Brettspiel - Neu & OVP","@type":"ImageObject"}</script>
          <p>120 €</p></article>
        """;

    [Test]
    public void Kleinanzeigen_ad_gives_title_price_and_link_and_skips_games()
    {
        var l = KleinanzeigenListings.ParseAds(Kleinanzeigen, "Die Farben der Magie", "Terry Pratchett", "u", 7.46m);
        l.Count.Should().Be(1);
        var o = l.All.Single();
        o.Name.Should().Be("Terry Pratchett – Die Farben der Magie, gebunden");
        o.Price.Should().Be(37m);
        o.Original.Should().Be("5,00 EUR VB");
        o.Url.Should().Be("https://www.kleinanzeigen.de/s-anzeige/die-farben-der-magie/1-76-1");
        KleinanzeigenListings.ParseAds(Kleinanzeigen, "Die Hexen", "Terry Pratchett", "u", 7.46m).Count.Should().Be(0);
    }
}
