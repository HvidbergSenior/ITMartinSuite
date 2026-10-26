using FluentAssertions;
using ITMartinLager.Application;
using ITMartinLager.Domain;
using ITMartinLager.Infrastructure.External;
using ITMartinLager.Infrastructure.Persistence;
using ITMartinLager.Infrastructure.PriceSites;

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

    // Trimmed Tradera REST v4 /search answer for "Anders And 1975" (2026-10-11): a shop item, an ended one, an auction, another magazine.
    private const string Tradera = """
        {"totalNumberOfItems":4,"items":[
          {"id":742593499,"shortDescription":"Anders And & Co 1975 Nr 27","buyItNowPrice":29,"maxBid":29,"nextBid":29,"isEnded":false,
           "itemUrl":"http://www.tradera.com/item/1124/742593499/anders-and-co-1975-nr-27"},
          {"id":1,"shortDescription":"Anders And & Co 1975 Nr 3","buyItNowPrice":10,"isEnded":true,"itemUrl":"http://www.tradera.com/item/1/1/x"},
          {"id":2,"shortDescription":"Anders And & Co 1975 Nr 12","buyItNowPrice":0,"maxBid":40,"nextBid":45,"isEnded":false,"itemUrl":"http://www.tradera.com/item/1/2/y"},
          {"id":3,"shortDescription":"Teknikens Värld 1975-26","buyItNowPrice":159,"isEnded":false,"itemUrl":"http://www.tradera.com/item/1/3/z"}
        ]}
        """;

    [Test]
    public void Tradera_takes_buy_now_or_current_bid_skips_ended_and_other_titles()
    {
        var l = TraderaListings.Parse(Tradera, "Anders And & Co 1975", "", "u", 0.65m);   // comics are hunted with their number
        l.All.Select(o => o.Price).Should().BeEquivalentTo(new decimal?[] { 19m, 26m });   // 29 SEK buy-now, 40 SEK bid
        l.All.First().Url.Should().Be("https://www.tradera.com/item/1124/742593499/anders-and-co-1975-nr-27");
        l.All.First().Original.Should().Be("29,00 SEK");
    }

    // Trimmed eBay Browse API item_summary/search answer (shape from the API docs): buy-now in EUR, an auction with only a bid, another book.
    private const string Ebay = """
        {"total":3,"itemSummaries":[
          {"itemId":"v1|1|0","title":"Terry Pratchett - Mort (Corgi paperback)","price":{"value":"4.50","currency":"EUR"},
           "itemWebUrl":"https://www.ebay.de/itm/1"},
          {"itemId":"v1|2|0","title":"Mort by Terry Pratchett 1987 first edition","currentBidPrice":{"value":"20.00","currency":"EUR"},
           "itemWebUrl":"https://www.ebay.de/itm/2"},
          {"itemId":"v1|3|0","title":"Guards! Guards! Terry Pratchett","price":{"value":"6.00","currency":"EUR"},"itemWebUrl":"https://www.ebay.de/itm/3"}
        ]}
        """;

    [Test]
    public void Ebay_takes_price_or_current_bid_and_skips_other_titles()
    {
        var l = EbayListings.Parse(Ebay, "Mort", "Terry Pratchett", "u", new Dictionary<string, decimal?> { ["EUR"] = 7.46m });
        l.All.Select(o => o.Price).Should().BeEquivalentTo(new decimal?[] { 34m, 149m });   // 4.50 EUR, 20 EUR bid
        l.All.First().Url.Should().Be("https://www.ebay.de/itm/1");
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
        Pricing.NormalOf([5, 5, 10, 10, 15, 400]).Should().Be(10);
        Pricing.NormalOf([]).Should().BeNull();
    }

    [Test]
    public void Far_off_prices_are_flagged_with_how_far()
    {
        Pricing.Flag(50, 5).Should().Be("10× normalprisen");
        Pricing.Flag(12, 10).Should().Be("");
        Pricing.Flag(3, 10).Should().Be("under en tredjedel af normalprisen");
        Pricing.Flag(50, null).Should().Be("");
    }

    [Test]
    public void Keeps_the_cheapest_the_middle_and_the_dearest()
    {
        var offers = Enumerable.Range(1, 30).Select(i => new Offer($"b{i}", i * 10m, $"u{i}")).ToList();
        var kept = Pricing.Keep(ListingParse.Summarise(offers, "u")).Select(o => (int)o.Price!.Value).ToList();
        kept.Should().HaveCount(12);
        kept.Should().Contain([10, 20, 290, 300]);
        kept.Should().Contain(160);
    }

    [Test]
    public void Comics_are_searched_with_their_number()
    {
        Pricing.TitleFor(new Item { Title = "", Series = "Anders And & Co.", Number = "12" }).Should().Be("Anders And & Co. 12");
        Pricing.TitleFor(new Item { Title = "Tintin 5", Number = "5" }).Should().Be("Tintin 5");
        Pricing.TitleFor(new Item { Title = "Mort" }).Should().Be("Mort");
    }

    [Test]
    public void Comics_get_the_Pegasus_link_other_things_do_not()
    {
        Pricing.PegasusFits(new Item { Kind = Kinds.Tegneserie }).Should().BeTrue();
        Pricing.PegasusFits(new Item { Kind = Kinds.AndersAnd }).Should().BeTrue();
        Pricing.PegasusFits(new Item { Kind = Kinds.Jumbobog }).Should().BeTrue();
        Pricing.PegasusFits(new Item { Kind = Kinds.Bog }).Should().BeFalse();
        Pricing.PegasusFits(new Item { Kind = Kinds.Cd }).Should().BeFalse();
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
        Pricing.ListingNumber("https://www.zvab.com/Mort-Terry-Pratchett-Corgi-Books/31992988332/bd").Should().Be("31992988332");
        Pricing.ListingNumber("https://www.abebooks.com/Mort-Terry-Pratchett-Corgi-Books/31992988332/bd").Should().Be("31992988332");
        Pricing.ListingNumber("https://www.dba.dk/recommerce/forsale/item/1945166").Should().Be("");
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

    // 2026-10-13: these got 0 hits on every site with the title search.
    [Test]
    public void Nothing_found_tries_series_and_number_then_series_and_title_then_short_title()
    {
        Pricing.FallbackTitlesFor(new Item { Kind = Kinds.Tegneserie, Title = "Vampyrernes nat", Series = "Sammy", Number = "5" })
            .Should().Equal("Sammy 5", "Sammy Vampyrernes nat");
        // Since 2026-10-26 a numbered blad is searched by series + number + year first, so nothing is left to fall back to.
        var blad = new Item { Kind = Kinds.AndersAnd, Title = "Walt Disney's Anders And & Co.", Series = "Anders And & Co.", Number = "3", Year = 1955 };
        Pricing.TitleFor(blad).Should().Be("Anders And & Co. 3 1955");
        Pricing.FallbackTitlesFor(blad).Should().BeEmpty();
        Pricing.FallbackTitlesFor(new Item { Kind = Kinds.Tegneserie, Title = "The Amazing Spider-Man: Spidey Saves the Day! The Big Book", Series = "The Amazing Spider-Man" })
            .Should().Equal("Spidey Saves the Day");
        Pricing.FallbackTitlesFor(new Item { Kind = Kinds.Bog, Title = "Mort: A Discworld Novel" }).Should().Equal("Mort");
        Pricing.FallbackTitlesFor(new Item { Kind = Kinds.Bog, Title = "Otto Reliquias" }).Should().BeEmpty();
    }

    [Test]
    public void A_comic_listing_may_start_with_the_series_name()
    {
        var sammy = new Item { Kind = Kinds.Tegneserie, Title = "Vampyrernes nat", Series = "Sammy", Number = "5" };
        Pricing.IsOneCopyOf(sammy, new Offer("Sammy 5 Vampyrernes nat", 40, "u")).Should().BeTrue();
        Pricing.IsOneCopyOf(sammy, new Offer("Spirou 5 Vampyrernes nat", 40, "u")).Should().BeFalse();
    }

    // 2026-10-26, Jørgen's and Marianne's first boxes.
    [Test]
    public void A_blad_is_searched_by_series_not_the_cover_headline()
    {
        var blad = new Item { Kind = Kinds.AndersAnd, Title = "Sluk lyset, hvis du tør!", Series = "Anders And & Co.", Number = "41", Year = 1987 };
        Pricing.TitleFor(blad).Should().Be("Anders And & Co. 41 1987");
        Pricing.SimilarTitleFor(blad).Should().Be("Anders And & Co. 1987");
    }

    [Test]
    public void A_blad_listing_must_say_Anders_And()
    {
        var blad = new Item { Kind = Kinds.AndersAnd, Title = "Anders And & Co.", Series = "Anders And & Co.", Number = "20", Year = 1985 };
        Pricing.IsOneCopyOf(blad, new Offer("Engel i sne af Anders Totland (f. 1986) (Bog)", 159, "u")).Should().BeFalse();
        Pricing.IsOneCopyOf(blad, new Offer("FN : vores sidste, bedste håb : samtaler om FN gennem 70 år af Anders Agner Pedersen (f. 1985)", 58, "u")).Should().BeFalse();
        Pricing.IsOneCopyOf(blad, new Offer("Anders And & Co. 1985 nummer 20", 8, "u")).Should().BeTrue();
    }

    [Test]
    public void Several_copies_titles_or_a_poster_are_not_one_copy()
    {
        var kalender = new Item { Kind = Kinds.AndersAnd, Title = "Julekalender", Series = "Anders And & Co.", Number = "47", Year = 1985 };
        Pricing.IsOneCopyOf(kalender, new Offer("2 Anders and Julekalendere som indlæg", 50, "u")).Should().BeFalse();

        var bog = new Item { Kind = Kinds.Bog, Title = "Genfærd", Artist = "Jo Nesbø" };
        Pricing.IsOneCopyOf(bog, new Offer("Jo Nesbø Rød hals, Politi, Genfærd, Panserhjerte, Kakerlakkerne, Kniv", 100, "u")).Should().BeFalse();
        Pricing.IsOneCopyOf(bog, new Offer("Jo Nesbø diverse krimi Headhunterne, Politi, Genfærd", 80, "u")).Should().BeFalse();
        Pricing.IsOneCopyOf(bog, new Offer("Jo Nesbø krimipakke – Kakerlakkerne, Frelseren, Genfærd, Marekors, Sorgenfri", 50, "u")).Should().BeFalse();
        Pricing.IsOneCopyOf(bog, new Offer("Genfærd, Jo Nesbø, genre: krimi og spænding", 40, "u")).Should().BeTrue();
        Pricing.IsOneCopyOf(bog, new Offer("Genfærd af Jo Nesbø (Bog)", 34, "u")).Should().BeTrue();

        var film = new Item { Kind = Kinds.Dvd, Title = "Olsen Banden på sporet" };
        Pricing.IsOneCopyOf(film, new Offer("Olsen Banden på sporet original biografplakat", 1000, "u")).Should().BeFalse();
        Pricing.IsOneCopyOf(film, new Offer("Olsen Banden på sporet", 20, "u")).Should().BeTrue();
    }

    [Test]
    public void An_unsure_reading_is_confirmed_by_a_listing_with_the_same_series_and_number()
    {
        var basserne = new Item { Kind = Kinds.Tegneserie, Title = "Basserne", Series = "Basserne", Number = "779", Confidence = 0.4 };
        Pricing.ConfirmsReading(basserne, "Basserne 779").Should().BeTrue();
        Pricing.ConfirmsReading(basserne, "Basserne 952").Should().BeFalse();

        var now = DateTime.UtcNow;
        var answers = new[] { new SourceAnswer("Nemos Bibliotek", false, ListingParse.Summarise([new Offer("Basserne 779", 13, "u")], "s")) };
        HuntResult.From(1, answers, now).ApplyTo(basserne, now);
        basserne.PriceHint.Should().NotBeNull();

        var unsure = new Item { Kind = Kinds.Tegneserie, Title = "Basserne", Series = "Basserne", Number = "778", Confidence = 0.4 };
        HuntResult.From(1, answers, now).ApplyTo(unsure, now);
        unsure.PriceHint.Should().BeNull();
    }

    [Test]
    public void No_copy_for_sale_asks_for_the_series_as_magen_til()
    {
        var rodoje = new Item { Kind = Kinds.Tegneserie, Title = "Rødøje", Series = "Basserne", Number = "612" };
        Pricing.TitleFor(Pricing.SeriesItemFor(rodoje)!).Should().Be("Basserne");
        Pricing.IsOneCopyOf(Pricing.SeriesItemFor(rodoje)!, new Offer("Basserne 952", 13, "u")).Should().BeTrue();
        // Faraos names the issue, not the story: "Basserne 2001 Nr. 612" is Rødøje.
        Pricing.IsOneCopyOf(rodoje, new Offer("Basserne 2001 Nr. 612", 18, "u", "Fine, 1. udgave, 1. oplag")).Should().BeTrue();
        Pricing.IsOneCopyOf(rodoje, new Offer("Basserne 2001 Nr. 611", 18, "u")).Should().BeFalse();
        Pricing.SeriesItemFor(new Item { Kind = Kinds.Tegneserie, Title = "Udvidet nummer", Series = "Basserne" }).Should().NotBeNull();
        Pricing.SeriesItemFor(new Item { Kind = Kinds.Bog, Title = "Genfærd", Series = "Harry Hole", Number = "9" }).Should().BeNull();

        // A broad answer keeps its listings aside - no price from it, but the series step can use them.
        var broad = ListingParse.Summarise([new Offer("Basserne 952", 13, "u1"), new Offer("Basserne 48", 28, "u2")], "s", ambiguous: true);
        broad.All.Should().BeEmpty();
        ListingParse.Summarise(broad.Broad, broad.Url).HasPrices.Should().BeTrue();
    }

    [Test]
    public void A_box_set_may_be_a_box_of_many_discs()
    {
        var matador = new Item { Kind = Kinds.Dvd, Title = "Matador – Matador Kollektionen 1929-1947", Note = "DR boks" };
        Pricing.IsOneCopyOf(matador, new Offer("Matador komplet boks 1-24, 12 dvds", 300, "u")).Should().BeTrue();
        Pricing.IsTheFilm(matador, "Matador komplet boks 1-24", needFormatWord: false).Should().BeTrue();

        var film = new Item { Kind = Kinds.Dvd, Title = "Underworld" };
        Pricing.IsOneCopyOf(film, new Offer("Underworld boks 1-4 dvds", 100, "u")).Should().BeFalse();
    }
}

public class ShowOnlyTests
{
    // 2026-10-26: "turn on abebooks but only show like 2 realistic bids from it".
    [Test]
    public void AbeBooks_shows_the_two_listings_nearest_its_middle_but_all_count()
    {
        var offers = new[] { 40, 60, 75, 80, 95, 120, 900 }.Select((kr, n) => new Offer($"Genfærd {n}", kr, $"u{n}")).ToList();
        var r = ListingParse.Summarise(offers, "s");
        var shown = Pricing.Keep(r, "AbeBooks");
        shown.Select(o => (int)o.Price!.Value).Should().Equal(75, 80);
        Pricing.Keep(r, "DBA").Should().HaveCount(7);

        var result = HuntResult.From(1, [new SourceAnswer("AbeBooks", false, r)], DateTime.UtcNow);
        result.Finds.Should().HaveCount(2);
        result.Normal.Should().NotBeNull();
    }
}

public class KioskTests
{
    // 2026-10-26: "what does it cost as new... Basserne right now and anders and blade".
    [Test]
    public void The_most_specific_series_wins()
    {
        KioskPrice[] prices = [new() { Series = "Anders And & Co.", Kr = 40 }, new() { Series = "Anders And Ekstra", Kr = 55 }, new() { Series = "Basserne", Kr = 45 }];
        Kiosk.For(new Item { Kind = Kinds.AndersAnd, Title = "Sluk lyset, hvis du tør!", Series = "Anders And & Co." }, prices)!.Kr.Should().Be(40);
        Kiosk.For(new Item { Kind = Kinds.AndersAnd, Title = "Anders And Ekstra", Series = "Anders And Ekstra" }, prices)!.Kr.Should().Be(55);
        Kiosk.For(new Item { Kind = Kinds.Tegneserie, Title = "Rødøje", Series = "Basserne" }, prices)!.Kr.Should().Be(45);
        Kiosk.For(new Item { Kind = Kinds.Bog, Title = "Genfærd" }, prices).Should().BeNull();
    }
}
