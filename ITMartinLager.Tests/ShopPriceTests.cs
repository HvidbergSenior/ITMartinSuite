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
