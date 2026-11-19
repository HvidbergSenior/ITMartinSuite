using FluentAssertions;
using ITMartinHjem.Application;
using ITMartinHjem.Domain;
using ITMartinVarer.Contracts;

namespace ITMartinHjem.Tests;

// Mine ønsker (2026-11-19): "Anders And" / "Bones DVD" find the right things, new finds pop up once.
public class WishTests
{
    [Test]
    public void A_kind_word_narrows_the_search()
    {
        WishRules.Parse("Bones DVD").Should().Be(("Bones", "DVD"));
        WishRules.Parse("jeg leder efter Anders And").Should().Be(("Anders And", (string?)null));
    }

    [Test]
    public void Every_word_must_be_there_and_the_kind_must_fit()
    {
        WishRules.Matches("Bones DVD", "Bones – sæson 2", "", "DVD").Should().BeTrue();
        WishRules.Matches("Bones DVD", "Bones – sæson 2", "", "Blu-ray").Should().BeFalse();
        WishRules.Matches("Anders And", "Anders And & Co. nr. 12", "1978", "Anders And-blad").Should().BeTrue();
        WishRules.Matches("Anders And", "Andersen-eventyr", "", "Bog").Should().BeFalse();
        WishRules.Matches("Pokémon", "Pokemon Gold", "", "Konsolspil").Should().BeTrue();
    }

    [Test]
    public void A_wish_needs_words_and_a_customer_has_at_most_20()
    {
        WishRules.Check(" ", 0).Should().NotBeNull();
        WishRules.Check("Tintin", WishRules.MaxPerCustomer).Should().NotBeNull();
        WishRules.Check("Tintin", 0).Should().BeNull();
    }

    [Test]
    public async Task Found_things_are_new_until_seen()
    {
        var store = new MemoryWishes();
        var uc = new WishUseCases(store, new OneShop());
        (await uc.AddAsync("12345678", "Bones DVD")).Should().BeNull();

        var found = await uc.FoundAsync("12345678");
        found.Should().ContainSingle(f => f.Item.Title == "Bones – sæson 2" && f.New);

        await uc.MarkSeenAsync("12345678", found.Select(f => f.Item.Id));
        (await uc.FoundAsync("12345678")).Single().New.Should().BeFalse();
        (await uc.FoundAsync("87654321")).Should().BeEmpty();
    }

    private sealed class MemoryWishes : ICustomerWishStore
    {
        private readonly List<CustomerWish> _w = [];
        private readonly List<(string C, int Id)> _seen = [];
        public Task<List<CustomerWish>> OfAsync(string customer, CancellationToken ct = default) => Task.FromResult(_w.Where(w => w.Customer == customer).ToList());
        public Task<List<string>> AllTextsAsync(CancellationToken ct = default) => Task.FromResult(_w.Select(w => w.Text).ToList());
        public Task AddAsync(CustomerWish wish, CancellationToken ct = default) { wish.Id = _w.Count + 1; _w.Add(wish); return Task.CompletedTask; }
        public Task RemoveAsync(string customer, int id, CancellationToken ct = default) { _w.RemoveAll(w => w.Customer == customer && w.Id == id); return Task.CompletedTask; }
        public Task<HashSet<int>> SeenAsync(string customer, CancellationToken ct = default) => Task.FromResult(_seen.Where(s => s.C == customer).Select(s => s.Id).ToHashSet());
        public Task MarkSeenAsync(string customer, IEnumerable<int> itemIds, CancellationToken ct = default) { _seen.AddRange(itemIds.Select(i => (customer, i))); return Task.CompletedTask; }
    }

    // Only SearchAsync is used; the shop answers "Bones" with a DVD and a Blu-ray.
    private sealed class OneShop : IShopGateway
    {
        private static ShopItem Item(int id, string title, string kind) =>
            new(id, kind, "", title, "", "God", 2008, null, null, 1, true);

        public Task<(bool CanPay, List<ShopItem> Items)> SearchAsync(string? text, string? kind, CancellationToken ct = default) =>
            Task.FromResult((false, new List<ShopItem> { Item(1, "Bones – sæson 2", "DVD"), Item(2, "Bones – sæson 3", "Blu-ray") }
                .Where(i => kind is null || i.Kind == kind).ToList()));
        public Task<(bool CanPay, ShopItem Item, List<ShopProof> Proof)?> ItemAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<byte[]?> PhotoAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<(ShopItem Item, List<ShopProof> Proof)>> FeaturedAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<(string? Url, string? Error)> BuyAsync(int id, string siteBase, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ShopOrder?> OrderAsync(string token, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ShopMarket> MarketAsync(string? type, string? kind, string? usage, string? text, string? sort, int page, CancellationToken ct = default,
            string? price = null, string? language = null, IReadOnlyCollection<string>? categories = null, string? genre = null) => throw new NotImplementedException();
        public Task<ShopOverview> OverviewAsync(int perShelf, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ShopQuote> QuoteAsync(IReadOnlyList<int> itemIds, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<(string? Url, string? Error)> BuyBasketAsync(IReadOnlyList<int> itemIds, string siteBase, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
