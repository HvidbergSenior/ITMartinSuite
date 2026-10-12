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
        ITMartinLager.Domain.Pricing.TitleFor(new ITMartinLager.Domain.Item { Kind = ITMartinLager.Domain.Kinds.AndersAnd, Title = "Anders And & Co.", Number = "12", Year = 1968 })
            .Should().Be("Anders And & Co. 12 1968");

    [Test]
    public void A_book_is_not_searched_with_its_year() =>
        ITMartinLager.Domain.Pricing.TitleFor(new ITMartinLager.Domain.Item { Kind = ITMartinLager.Domain.Kinds.Bog, Title = "Mort", Year = 1987 })
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
        ITMartinLager.Domain.Pricing.AlternativeTitleFor(new ITMartinLager.Domain.Item { Kind = ITMartinLager.Domain.Kinds.Tegneserie, Title = "Storm Petersen Album", Number = "29", Year = 1941 })
            .Should().Be("Storm Petersen Album 1941");

    [Test]
    public void Without_a_number_there_is_no_second_search() =>
        ITMartinLager.Domain.Pricing.AlternativeTitleFor(new ITMartinLager.Domain.Item { Kind = ITMartinLager.Domain.Kinds.Bog, Title = "Mort", Year = 1987 })
            .Should().BeNull();
}

public class SimilarSearchTests
{
    private static ITMartinLager.Domain.Item I(string kind, string title, string number = "", int? year = null) =>
        new() { Kind = kind, Title = title, Number = number, Year = year };

    [Test]
    public void An_album_is_compared_with_the_same_album_in_other_years() =>
        ITMartinLager.Domain.Pricing.SimilarTitleFor(I(ITMartinLager.Domain.Kinds.Tegneserie, "Storm Petersen Album", "29", 1941))
            .Should().Be("Storm Petersen Album");

    [Test]
    public void An_Anders_And_blad_is_compared_with_other_issues_of_the_same_year() =>
        ITMartinLager.Domain.Pricing.SimilarTitleFor(I(ITMartinLager.Domain.Kinds.AndersAnd, "Anders And & Co.", "12", 1968))
            .Should().Be("Anders And & Co. 1968");

    [Test]
    public void A_plain_title_has_nothing_broader() =>
        ITMartinLager.Domain.Pricing.SimilarTitleFor(I(ITMartinLager.Domain.Kinds.Bog, "Mort")).Should().BeNull();

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
