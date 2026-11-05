using FluentAssertions;
using ITMartinHjem.Application;
using ITMartinVarer.Contracts;

namespace ITMartinHjem.Tests;

// Markedsplads on bogshoppen.dk (2026-11-05): Byd + Mit bundt.
public class MarketOfferTests
{
    [Test]
    public void Mit_bundt_toggles_keeps_the_order_and_is_per_customer()
    {
        var d = new BundleDrafts();
        d.Toggle("11112222", 40); d.Toggle("11112222", 12); d.Toggle("33334444", 7);
        d.Of("11112222").Should().Equal(40, 12);
        d.Toggle("11112222", 40);
        d.Of("11112222").Should().Equal(12);
        d.Has("33334444", 7).Should().BeTrue();
        d.Clear("11112222");
        d.Of("11112222").Should().BeEmpty();
    }

    [Test]
    public void Mit_bundt_holds_at_most_50()
    {
        var d = new BundleDrafts();
        for (var i = 1; i <= 60; i++) d.Toggle("11112222", i);
        d.Of("11112222").Should().HaveCount(50);
    }

    private sealed class FakeOffers : IOfferGateway
    {
        public int Bids, Bundles; public int? LastKr; public int LastCount;
        public Task<ShopOfferAnswer> BidAsync(string customer, int itemId, int maxKr, CancellationToken ct = default) { Bids++; return Task.FromResult(new ShopOfferAnswer(true)); }
        public Task<Dictionary<int, int>> MyBidsAsync(string customer, CancellationToken ct = default) => Task.FromResult(new Dictionary<int, int>());
        public Task<ShopOfferAnswer> BundleAsync(string customer, IReadOnlyList<int> itemIds, int? kr, CancellationToken ct = default)
        { Bundles++; LastKr = kr; LastCount = itemIds.Count; return Task.FromResult(new ShopOfferAnswer(true)); }
    }
    private sealed class NoCodes : IMarketCodes { public Task<(bool Ok, string Error)> CheckAsync(string code, CancellationToken ct = default) => Task.FromResult((true, "")); }
    private sealed class NoInterests : IInterestGateway
    {
        public Task<ShopInterest> ToggleAsync(string customer, int itemId, CancellationToken ct = default) => Task.FromResult(new ShopInterest(true, 1));
        public Task<List<int>> MineAsync(string customer, CancellationToken ct = default) => Task.FromResult(new List<int>());
    }

    private static (MarketUseCases Uc, FakeOffers Offers) Make()
    {
        var offers = new FakeOffers();
        return (new MarketUseCases(new NoCodes(), new NoInterests(), offers), offers);
    }

    [Test]
    public async Task Byd_needs_a_login_and_an_amount_before_Salg_is_asked()
    {
        var (uc, offers) = Make();
        (await uc.BidAsync("", 12, 80)).Error.Should().Contain("Log ind");
        (await uc.BidAsync("11112222", 12, null)).Error.Should().Contain("højst");
        offers.Bids.Should().Be(0);
        (await uc.BidAsync("11112222", 12, 80)).Ok.Should().BeTrue();
    }

    [Test]
    public async Task A_bundle_is_at_least_two_things_and_may_be_a_question()
    {
        var (uc, offers) = Make();
        (await uc.BundleAsync("11112222", [12], 50)).Error.Should().Contain("mindst 2");
        (await uc.BundleAsync("11112222", [12, 40], null)).Ok.Should().BeTrue();
        offers.Bundles.Should().Be(1);
        offers.LastCount.Should().Be(2);
        offers.LastKr.Should().BeNull();
    }
}
