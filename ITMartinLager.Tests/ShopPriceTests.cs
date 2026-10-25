using FluentAssertions;

namespace ITMartinLager.Tests;

// "We sell 10 pct below other sites" (user 2026-10-12).
public class ShopPriceTests
{
    [TestCase(50, 45)]
    [TestCase(100, 90)]
    [TestCase(57, 50)]    // 51.3 -> down to 50, never above 90 %
    [TestCase(11, 10)]
    [TestCase(3, 3)]      // a cheap Magic card keeps whole kroner
    [TestCase(1, 1)]
    public void The_shop_price_is_10_percent_under_the_market(int market, int shop) =>
        ITMartinLager.Domain.Pricing.ShopPrice(market).Should().Be(shop);

    [Test]
    public void No_market_means_no_shop_price() => ITMartinLager.Domain.Pricing.ShopPrice(null).Should().BeNull();
}

// 2026-10-12 test: an Anders And blad is one issue of one year.
public class IssueSearchTests
{
    [Test]
    public void An_Anders_And_blad_is_searched_with_number_and_year() =>
        ITMartinLager.Domain.Pricing.TitleFor(new ITMartinLager.Domain.Item { Kind = Kinds.AndersAnd, Title = "Anders And & Co.", Number = "12", Year = 1968 })
            .Should().Be("Anders And & Co. 12 1968");

    [Test]
    public void A_book_is_not_searched_with_its_year() =>
        ITMartinLager.Domain.Pricing.TitleFor(new ITMartinLager.Domain.Item { Kind = Kinds.Bog, Title = "Mort", Year = 1987 })
            .Should().Be("Mort");

    [Test]
    public void A_listing_from_another_year_is_no_proof()
    {
        var m = ITMartinLager.Domain.ListingParse.Matcher("Anders And & Co. 12 1968", "");
        m.Matches("Flot Anders And & Co. 12+13/2021, Disney").Should().BeFalse();
        m.Matches("Anders And & Co nr. 12 1968").Should().BeTrue();
    }
}

public class AlternativeSearchTests
{
    [Test]
    public void An_album_with_aargang_and_year_is_searched_again_with_the_year() =>
        ITMartinLager.Domain.Pricing.AlternativeTitleFor(new ITMartinLager.Domain.Item { Kind = Kinds.Tegneserie, Title = "Storm Petersen Album", Number = "29", Year = 1941 })
            .Should().Be("Storm Petersen Album 1941");

    [Test]
    public void Without_a_number_there_is_no_second_search() =>
        ITMartinLager.Domain.Pricing.AlternativeTitleFor(new ITMartinLager.Domain.Item { Kind = Kinds.Bog, Title = "Mort", Year = 1987 })
            .Should().BeNull();
}

public class SimilarSearchTests
{
    private static ITMartinLager.Domain.Item I(string kind, string title, string number = "", int? year = null) =>
        new() { Kind = kind, Title = title, Number = number, Year = year };

    [Test]
    public void An_album_is_compared_with_the_same_album_in_other_years() =>
        ITMartinLager.Domain.Pricing.SimilarTitleFor(I(Kinds.Tegneserie, "Storm Petersen Album", "29", 1941))
            .Should().Be("Storm Petersen Album");

    [Test]
    public void An_Anders_And_blad_is_compared_with_other_issues_of_the_same_year() =>
        ITMartinLager.Domain.Pricing.SimilarTitleFor(I(Kinds.AndersAnd, "Anders And & Co.", "12", 1968))
            .Should().Be("Anders And & Co. 1968");

    [Test]
    public void A_plain_title_has_nothing_broader() =>
        ITMartinLager.Domain.Pricing.SimilarTitleFor(I(Kinds.Bog, "Mort")).Should().BeNull();

    [Test]
    public void Magen_til_never_sets_our_price()
    {
        var item = new ITMartinLager.Domain.Item();
        var like = new ITMartinLager.Domain.Listings(2, 50, 75, 100, "u", []) { All = [new("Storm Petersen Album 1993", 50, "a"), new("Storm Petersen Album 1950", 100, "b")] };
        var r = ITMartinLager.Domain.HuntResult.From(1, [new("DBA", false, new ITMartinLager.Domain.Listings(0, null, null, null, "u", []))], DateTime.UtcNow, [new("DBA", false, like)]);
        r.ApplyTo(item, DateTime.UtcNow);
        item.PriceHint.Should().BeNull();
        item.SimilarHint.Should().Be(100);
        r.Finds.Should().OnlyContain(f => f.Similar);
    }
}

// 2026-10-12: Spider-Geddon #3 was priced from other series, sets, graded copies and postcards. Real listing names.
public class OneCopyTests
{
    private static readonly ITMartinLager.Domain.Item Spider = new() { Kind = Kinds.Tegneserie, Title = "Spider-Geddon", Series = "Marvel", Number = "3" };
    private static bool Keep(string name) => ITMartinLager.Domain.Pricing.IsOneCopyOf(Spider, new ITMartinLager.Domain.Offer(name, 30, "u"));

    [TestCase("Spider-Geddon #3 - Regular Cover - Marvel Comics - 2019", true)]
    [TestCase("Spider-Geddon #3 NM+ 2019 Marvel Comics High Grade", false)]   // NM+ = a graded collector copy (2026-10-21)
    [TestCase("✪ SPIDER-GEDDON #3, MARVEL COMICS COMIC POSTCARD/POSTCARD NEW", false)]
    [TestCase("Edge of Spider-Geddon #3 Nov.2018 CVR A", false)]
    [TestCase("Spider-Girls #1,2,3 FULL SET Marvel Comics 2018 SPIDER-GEDDON SPIDER-WOMAN", false)]
    [TestCase("Spider-Geddon #1 2 3 4 5 - 2018 Spider Geddon 1-5 Run Set Lot - Marvel NM", false)]
    [TestCase("Spider-Geddon # 3 CGC 9.8 Torque 1:50 Incentive Variant Cover (2019)", false)]
    [TestCase("Spider-Geddon # 3 Hawthorne 1:25 Variant Cover NM- Marvel 2018", false)]
    [TestCase("Marvel Spider-Geddon 3", true)]
    public void Only_one_ordinary_copy_of_this_comic_counts(string name, bool kept) => Keep(name).Should().Be(kept);

    // 2026-10-25, Eigil's box K0001: real listing names the hunter kept as proof.
    private static bool KeepFor(ITMartinLager.Domain.Item item, string name) =>
        ITMartinLager.Domain.Pricing.IsOneCopyOf(item, new ITMartinLager.Domain.Offer(name, 30, "u"));

    [TestCase("Eddie Izzard / Dress To Kill", true)]
    [TestCase("Eddie Izzard Dress to Kill Ad Print P-216305", false)]
    public void A_stand_up_dvd_is_not_its_magazine_ad(string name, bool kept) =>
        KeepFor(new() { Kind = Kinds.Dvd, Title = "Dress to Kill", Artist = "Eddie Izzard" }, name).Should().Be(kept);

    [TestCase("The Chris Rock Show - Sesong 1 & 2", true)]
    [TestCase("Vintage The Chris Rock Show Cap OSFA Snap 90's OG HBO Logo SNL Comedy", false)]
    [TestCase("THE CHRIS ROCK SHOW METALL LOGO REISSVERSCHLUSS ZIEHER TV SHOW FERNSEH", false)]
    [TestCase("2001 Fleer WWF Raw is War Chris Jericho The Rock Show Enders WWE #89", false)]
    public void A_dvd_is_not_merchandise_of_the_show(string name, bool kept) =>
        KeepFor(new() { Kind = Kinds.Dvd, Title = "The Chris Rock Show", Artist = "Chris Rock" }, name).Should().Be(kept);

    [TestCase("Racetrack Babies - Love Sick (2002) – 11 til salg", true)]
    [TestCase("Ty Beanie Babys Love Sick & Jungle Love. Japan Seratti Haus", false)]
    public void A_cd_needs_its_artist(string name, bool kept) =>
        KeepFor(new() { Kind = Kinds.Cd, Title = "Love Sick", Artist = "Racetrack Babies" }, name).Should().Be(kept);

    [TestCase("The Spectacular Spider-Man #242 Marvel Comic January 1997 Face of Defeat", true)]
    [TestCase("2004 Topps Star Wars: Clone Wars The Face of Defeat #61 yf0", false)]
    public void A_comic_needs_its_series(string name, bool kept) =>
        KeepFor(new() { Kind = Kinds.Superhelte, Title = "The Face of Defeat...", Series = "The Spectacular Spider-Man" }, name).Should().Be(kept);

    [TestCase("The Walking Dead", true)]
    [TestCase("The Walking Dead - Xbox 360", false)]
    [TestCase("The walking dead - Season 1", false)]
    [TestCase("The WALKING DEAD Billet Million Dollar US Serie Zombie", false)]
    public void A_comic_is_not_the_tv_series_or_the_game(string name, bool kept) =>
        KeepFor(new() { Kind = Kinds.Superhelte, Title = "The Walking Dead – Two Day Special", Series = "The Walking Dead" }, name).Should().Be(kept);

    [TestCase("Birger og Viola 3 Som i et æventyr", true)]
    [TestCase("Søde Sally Sukkertop og andre ækle æventyr 3", false)]
    public void A_comic_album_is_not_another_book_with_a_title_word(string name, bool kept) =>
        KeepFor(new() { Kind = Kinds.Tegneserie, Title = "Som i et æventyr", Series = "Birger og Viola", Number = "3" }, name).Should().Be(kept);

    [TestCase("Kasket Karl Med På Den Værste, Reg Smythe,", true)]
    [TestCase("Kasket Karl 15 stk,", false)]
    [TestCase("2 stk tegneserier..KASKET KARL.", false)]
    public void Several_copies_in_one_listing_are_not_one_copy(string name, bool kept) =>
        KeepFor(new() { Kind = Kinds.Tegneserie, Title = "Kasket Karl – Med på den værste" }, name).Should().Be(kept);

    // 2026-10-25: "Guardians of the Galaxy also finds puslespil" + "if the price is for the product plus anything else".
    [TestCase("Guardians of the Galaxy DVD", true)]
    [TestCase("Guardians of the Galaxy puslespil 1000 brikker", false)]
    [TestCase("Guardians of the Galaxy Puzzle 500 Teile", false)]
    [TestCase("Guardians of the Galaxy + Avengers DVD", false)]
    [TestCase("Guardians of the Galaxy og Avengers Endgame", false)]
    [TestCase("Guardians of the Galaxy & Thor Ragnarok", false)]
    [TestCase("Guardians of the Galaxy inkl. plakat", false)]
    [TestCase("Guardians of the Galaxy med danske undertekster", true)]
    [TestCase("Guardians of the Galaxy dvd med cover og booklet", true)]
    public void A_film_is_not_a_puzzle_or_a_bundle(string name, bool kept) =>
        KeepFor(new() { Kind = Kinds.Dvd, Title = "Guardians of the Galaxy" }, name).Should().Be(kept);

    [TestCase("The Andrews Sisters - Rum And Coca Cola 20 Greatest Hits", true)]
    public void And_in_the_title_is_not_a_bundle(string name, bool kept) =>
        KeepFor(new() { Kind = Kinds.Cd, Title = "Rum And Coca Cola - 20 Greatest Hits", Artist = "Andrews Sisters" }, name).Should().Be(kept);

    [Test]
    public void Links_lose_the_tracking_bits_and_one_ebay_listing_is_one_link()
    {
        ITMartinLager.Domain.Pricing.CleanUrl("https://www.ebay.de/itm/306342744192?_skw=Rum&hash=item4753735080:g:-")
            .Should().Be("https://www.ebay.de/itm/306342744192");
        ITMartinLager.Domain.Pricing.ListingKey("https://www.ebay.com/itm/257778590325?_skw=x")
            .Should().Be(ITMartinLager.Domain.Pricing.ListingKey("https://www.ebay.de/itm/257778590325?hash=y"));
        ITMartinLager.Domain.Pricing.QueryFor("The Chris Rock Show", "Chris Rock").Should().Be("The Chris Rock Show");
        ITMartinLager.Domain.Pricing.CleanTitle("The Face of Defeat...").Should().Be("The Face of Defeat");
    }

    [Test]
    public void A_book_by_author_first_still_counts() =>
        ITMartinLager.Domain.Pricing.IsOneCopyOf(new ITMartinLager.Domain.Item { Kind = Kinds.Bog, Title = "Mort", Artist = "Terry Pratchett" },
            new ITMartinLager.Domain.Offer("Terry Pratchett - Mort (Corgi 1988)", 40, "u")).Should().BeTrue();

    [Test]
    public void Danish_comics_with_publisher_first_still_count() =>
        ITMartinLager.Domain.Pricing.IsOneCopyOf(new ITMartinLager.Domain.Item { Kind = Kinds.Tegneserie, Title = "Tintin", Number = "5" },
            new ITMartinLager.Domain.Offer("Carlsen Tintin nr. 5 Faraos cigarer", 300, "u")).Should().BeTrue();
}
