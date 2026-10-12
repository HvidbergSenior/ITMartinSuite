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
