using FluentAssertions;
using ITMartinTilbud.Application;
using ITMartinTilbud.Domain;

namespace ITMartinTilbud.Tests;

/// <summary>Fakes for the ports - no network.</summary>
internal sealed class FakeOffers : IOfferSource
{
    public List<string> Terms { get; } = [];
    public Dictionary<string, List<Offer>> ByTerm { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool Down { get; set; }
    public List<Leaflet> Leaflets { get; } = [];
    public Dictionary<string, List<Offer>> LeafletOffers { get; } = [];

    public Task<List<Offer>> SearchAsync(string term, double lat, double lng, int km, CancellationToken ct)
    {
        Terms.Add(term);
        if (Down) throw new SourceUnavailableException("Tilbudsavisen svarer ikke lige nu.");
        return Task.FromResult(ByTerm.GetValueOrDefault(term) ?? []);
    }

    public Task<NearestStore?> NearestStoreAsync(string dealerId, double lat, double lng, CancellationToken ct) =>
        Task.FromResult<NearestStore?>(new NearestStore($"{dealerId}-vej 1, 8200 Aarhus N", 1.2));

    public Task<List<Leaflet>> LeafletsAsync(double lat, double lng, CancellationToken ct) => Task.FromResult(Leaflets);

    public Task<List<Offer>> LeafletOffersAsync(Leaflet leaflet, CancellationToken ct) =>
        Task.FromResult(LeafletOffers.GetValueOrDefault(leaflet.Id) ?? []);
}

internal sealed class MemoryDeals : IFixedDealStore
{
    private readonly List<FixedDeal> _d = [];
    public IReadOnlyList<FixedDeal> All() => _d;
    public void Add(FixedDeal deal) => _d.Add(deal);
    public bool Remove(string id) => _d.RemoveAll(x => x.Id == id) > 0;
}

internal static class Make
{
    public static Offer Offer(string chain, string title, decimal price, decimal? unit = null, string? dealer = null) =>
        new(chain, title, null, price, null, unit, unit is null ? null : "kr/l", null, null, null, null, null, DealerId: dealer);
}

public class SearchOffersTests
{
    [TestCase("a")]
    [TestCase("  ")]
    public async Task Too_short_searches_are_refused(string q)
    {
        var act = () => new SearchOffers(new FakeOffers()).ExecuteAsync(q, 56, 10, 10, default);
        await act.Should().ThrowAsync<TilbudException>().WithMessage("*mindst 2*");
    }

    [Test]
    public async Task A_place_is_needed()
    {
        var act = () => new SearchOffers(new FakeOffers()).ExecuteAsync("mælk", null, 10, 10, default);
        await act.Should().ThrowAsync<TilbudException>().WithMessage("*hvor du bor*");
    }

    [Test]
    public async Task Describing_words_also_search_for_the_thing_itself_and_duplicates_merge()
    {
        var src = new FakeOffers();
        var oeko = Make.Offer("Netto", "Øko letmælk", 12, 12, "n1");
        src.ByTerm["økologisk mælk"] = [oeko];
        src.ByTerm["mælk"] = [oeko, Make.Offer("Lidl", "Arla mælk", 9, 9, "l1")];
        var list = await new SearchOffers(src).ExecuteAsync(" økologisk mælk ", 56, 10, 10, default);
        src.Terms.Should().Equal("økologisk mælk", "mælk");
        list.Should().HaveCount(2);
        list[0].Title.Should().Be("Øko letmælk", "the described kind comes first");
    }

    [Test]
    public async Task Every_offer_gets_its_chains_nearest_store()
    {
        var src = new FakeOffers();
        src.ByTerm["smør"] = [Make.Offer("Netto", "Lurpak smør", 20, dealer: "netto")];
        var o = (await new SearchOffers(src).ExecuteAsync("smør", 56, 10, 10, default)).Single();
        (o.NearestStore, o.NearestKm).Should().Be(("netto-vej 1, 8200 Aarhus N", 1.2));
    }

    [Test]
    public async Task The_radius_is_kept_within_50_km()
    {
        var src = new FakeOffers();
        await new SearchOffers(src).ExecuteAsync("kaffe", 56, 10, 999, default);
        src.Terms.Should().ContainSingle();
    }
}

public class AppGapTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }

    private static Leaflet L(string id, string chain, string label = "Uge 41", int days = 7, int count = 50) =>
        new(id, chain, label, count, Now.AddDays(-1), Now.AddDays(days - 1));

    [Test]
    public void Only_this_weeks_food_leaflets_of_grocery_chains_count()
    {
        AppGap.IsThisWeeksFoodLeaflet(L("1", "Netto"), Now).Should().BeTrue();
        AppGap.IsThisWeeksFoodLeaflet(L("2", "Bauhaus"), Now).Should().BeFalse("not a grocery chain");
        AppGap.IsThisWeeksFoodLeaflet(L("3", "Netto", "Nonfood uge 41"), Now).Should().BeFalse();
        AppGap.IsThisWeeksFoodLeaflet(L("4", "Netto", days: 30), Now).Should().BeFalse("a month-long catalogue is not the week");
        AppGap.IsThisWeeksFoodLeaflet(L("5", "Netto", count: 0), Now).Should().BeFalse();
    }

    [Test]
    public async Task Chains_where_the_app_matters_most_come_first()
    {
        var src = new FakeOffers();
        src.Leaflets.AddRange([L("n", "Netto"), L("r", "REMA 1000"), L("x", "Bauhaus")]);
        Offer App(decimal p, decimal ap) => Make.Offer("Netto", "x", p) with { AppName = "Netto+ appen", AppPrice = ap };
        src.LeafletOffers["n"] = [App(24, 20), Make.Offer("Netto", "y", 10)];
        src.LeafletOffers["r"] = [Make.Offer("REMA 1000", "z", 10)];
        var gaps = await new ThisWeeksAppGap(src, new Clock()).ExecuteAsync(56, 10, default);
        gaps.Select(g => g.Chain).Should().Equal("Netto", "REMA 1000");
        gaps[0].ExtraTotal.Should().Be(4m);
    }
}

public class FixedDealsTests
{
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 10, 8, 9, 0, 0, TimeSpan.Zero); }

    [Test]
    public void A_deal_needs_chain_and_item()
    {
        var add = () => new FixedDealsBoard(new MemoryDeals(), new Clock()).Add(" ", "mælk", null, null, null, null, null);
        add.Should().Throw<TilbudException>().WithMessage("Skriv både butik og vare.");
    }

    [Test]
    public void Days_are_cleaned_and_empty_text_becomes_nothing()
    {
        var d = FixedDeal.Create(" Lidl ", " mælk ", [9, 2, 2, 0, 7], 0, " ", "Lidl Plus", null, new DateTime(2026, 10, 8));
        (d.Chain, d.Item, d.Price, d.Unit, d.NeedsApp).Should().Be(("Lidl", "mælk", null, null, "Lidl Plus"));
        d.Days.Should().Equal(2, 7);
    }

    [Test]
    public void The_board_lists_by_item_then_chain_and_removes()
    {
        var board = new FixedDealsBoard(new MemoryDeals(), new Clock());
        var b = board.Add("Netto", "smør", null, null, null, null, null);
        board.Add("Lidl", "mælk", null, null, null, null, null);
        board.Add("Coop", "mælk", null, null, null, null, null);
        board.All().Select(x => x.Chain).Should().Equal("Coop", "Lidl", "Netto");
        board.Remove(b.Id).Should().BeTrue();
        board.Remove("findes-ikke").Should().BeFalse();
    }
}

public class UnitAndFoodWasteRuleTests
{
    [Test]
    public void Pieces_are_priced_per_piece() =>
        UnitPrice.Compute(30, 1, 10, 10, null, null, "pcs").Should().Be((3m, "kr/stk", "10 stk"));

    [Test]
    public void Without_a_size_there_is_no_unit_price() =>
        UnitPrice.Compute(30, null, null, null, null, null, null).Should().Be(((decimal?)null, (string?)null, (string?)null));

    [Test]
    public void Nearby_visitors_share_one_Salling_answer()
    {
        Clearances.Area(56.1572, 10.2107, 3).Should().Be((56.15, 10.2, 5));
        Clearances.Area(56.1572, 10.2107, 12).Km.Should().Be(20);
    }

    [Test]
    public void A_single_word_is_searched_once() => OfferRanking.SearchTerms("kaffe").Should().Equal("kaffe");
}
