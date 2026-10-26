using FluentAssertions;
using ITMartinHjem.Application;

namespace ITMartinHjem.Tests;

// The marketplace for customers (2026-10-26): log in with the kundenummer (Kunder says yes or no), mark things in Salg.
public class MarketTests
{
    private sealed class Codes(string works) : IMarketCodes
    {
        public readonly List<string> Asked = [];
        public Task<(bool Ok, string Error)> CheckAsync(string code, CancellationToken ct = default)
        {
            Asked.Add(code);
            return Task.FromResult(code == works ? (true, "") : (false, "Det kundenummer kender vi ikke."));
        }
    }

    private sealed class Salg(bool down = false) : IInterestGateway
    {
        public Task<ShopInterest> ToggleAsync(string customer, int itemId, CancellationToken ct = default) =>
            down ? throw new HttpRequestException() : Task.FromResult(new ShopInterest(true, 3));
        public Task<List<int>> MineAsync(string customer, CancellationToken ct = default) =>
            down ? throw new HttpRequestException() : Task.FromResult(new List<int> { 7, 9 });
    }

    [Test]
    public async Task The_kundenummer_is_typed_any_way_and_Kunder_decides()
    {
        var codes = new Codes("48217730");
        var market = new MarketUseCases(codes, new Salg());

        (await market.LoginAsync(" 4821 7730 ")).Should().Be(("48217730", (string?)null));
        (await market.LoginAsync("11112222")).Should().Be(((string?)null, "Det kundenummer kender vi ikke."));
        (await market.LoginAsync("482")).Error.Should().Contain("8 cifre");
        codes.Asked.Should().Equal("48217730", "11112222");   // a too short number is never sent to Kunder
    }

    [Test]
    public async Task When_Salg_is_down_the_page_still_works()
    {
        var market = new MarketUseCases(new Codes(""), new Salg(down: true));
        (await market.ToggleAsync("48217730", 7)).Error.Should().Be(MarketUseCases.Unreachable);
        (await market.MineAsync("48217730")).Should().BeEmpty();
        (await new MarketUseCases(new Codes(""), new Salg()).MineAsync("48217730")).Should().BeEquivalentTo([7, 9]);
    }

    [Test]
    public void Customers_see_the_market_only_when_it_is_switched_on_and_never_open_the_basket()
    {
        new ShopVisibility(false, "proeve-2026").Customers.Should().BeFalse();
        var market = new ShopVisibility(false, "proeve-2026", Customers: true);
        market.Customers.Should().BeTrue();
        market.Visible(null).Should().BeFalse();   // Visible = Køb nu and the basket - still only the preview code
    }
}
