using FluentAssertions;
using ITMartinHjem.Application;

namespace ITMartinHjem.Tests;

public class ShopTests
{
    private sealed class FakeLager(bool down = false) : IShopGateway
    {
        public int Buys;
        private static ShopItem Mort => new(1, "Bog", "Mort · Terry Pratchett", "Mort", "", "God", 1987, 80, 80, 1, false);
        public Task<(bool CanPay, List<ShopItem> Items)> SearchAsync(string? text, string? kind, CancellationToken ct = default) =>
            down ? throw new HttpRequestException() : Task.FromResult((true, new List<ShopItem> { Mort }));
        public Task<(bool CanPay, ShopItem Item, List<ShopProof> Proof)?> ItemAsync(int id, CancellationToken ct = default) =>
            Task.FromResult<(bool, ShopItem, List<ShopProof>)?>((true, Mort, []));
        public Task<byte[]?> PhotoAsync(int id, CancellationToken ct = default) => Task.FromResult<byte[]?>(null);
        public Task<List<(ShopItem Item, List<ShopProof> Proof)>> FeaturedAsync(CancellationToken ct = default) =>
            down ? throw new HttpRequestException() : Task.FromResult(new List<(ShopItem, List<ShopProof>)> { (Mort, []) });
        public Task<(string? Url, string? Error)> BuyAsync(int id, string siteBase, CancellationToken ct = default)
        {
            Buys++;
            return Task.FromResult<(string?, string?)>(("https://checkout.stripe.com/x", null));
        }
        public Task<ShopOrder?> OrderAsync(string token, CancellationToken ct = default) => Task.FromResult<ShopOrder?>(null);
    }

    [Test]
    public async Task Buy_now_stays_hidden_until_the_seller_is_named_by_company_and_CVR()
    {
        var lager = new FakeLager();
        var nameless = new ShopUseCases(lager, new ShopSeller("", "", "", "a@b.dk", ""));
        (await nameless.SearchAsync("mort", null)).CanBuy.Should().BeFalse();
        (await nameless.BuyAsync(1, "https://bogshoppen.dk")).Error.Should().NotBeNull();
        lager.Buys.Should().Be(0);

        var named = new ShopUseCases(lager, new ShopSeller("Firma", "12345678", "", "a@b.dk", ""));
        (await named.SearchAsync("mort", null)).CanBuy.Should().BeTrue();
        (await named.BuyAsync(1, "https://bogshoppen.dk")).Url.Should().StartWith("https://checkout.stripe.com");
    }

    [Test]
    public async Task Lager_down_gives_a_message_not_an_error_page()
    {
        var (_, items, error) = await new ShopUseCases(new FakeLager(down: true), new ShopSeller("F", "1", "", "", "")).SearchAsync("x", null);
        items.Should().BeEmpty();
        error.Should().Be(ShopUseCases.Unreachable);
    }
}
