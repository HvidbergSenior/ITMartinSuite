using Moq;
using ITMartinLager.Application;
using FluentAssertions;
using ITMartinLager.Domain;

namespace ITMartinLager.Tests;

// Stikprøve (2026-11-24): a listing marked Forkert never counts again; the stats show how good each source is.
public class ListingVerdictTests
{
    private static PriceFind F(int item, string src, int kr, string url) => new() { ItemId = item, Source = src, Kr = kr, Url = url, Name = "x" };

    [Test]
    public void A_wrong_listing_is_left_out_for_that_thing_only()
    {
        var a = F(1, "DBA", 1000, "https://www.dba.dk/a"); var b = F(1, "Faraos", 30, "https://faraos.dk/b"); var c = F(2, "DBA", 1000, "https://www.dba.dk/a");
        var verdicts = new[] { new ListingVerdict { ItemId = 1, ListingKey = ListingVerdict.KeyOf(a), Right = false } };
        ListingVerdict.WithoutWrong([a, b, c], verdicts).Should().BeEquivalentTo([b, c]);
    }

    [Test]
    public void A_listing_without_an_address_still_gets_a_key() =>
        ListingVerdict.KeyOf(new PriceFind { Source = "DBA", Name = " Batman nr 7 ", Kr = 55 }).Should().Be("DBA|batman nr 7|55");

    [Test]
    public void Stats_count_right_and_wrong_per_source() =>
        ListingVerdict.PerSource([new() { Source = "DBA", Right = false }, new() { Source = "DBA", Right = true }, new() { Source = "Faraos", Right = true }])
            .Should().BeEquivalentTo(new[] { ("DBA", 1, 1), ("Faraos", 1, 0) });

    // "Kan ikke se" (2026-11-24): the listing still counts, and it is not in the right/wrong %.
    [Test]
    public void Cant_see_is_neither_right_nor_wrong()
    {
        var a = new PriceFind { ItemId = 1, Source = "DBA", Name = "A", Kr = 10 };
        var v = new ListingVerdict { ItemId = 1, ListingKey = ListingVerdict.KeyOf(a), Source = "DBA", CantSee = true };
        ListingVerdict.WithoutWrong([a], [v]).Should().ContainSingle();
        ListingVerdict.PerSource([v]).Should().BeEmpty();
    }

    [Test]
    public void Only_cds_above_standard_are_in_the_sample()
    {
        SpotCheckUseCases.Worth(new Item { Kind = Kinds.Cd, Title = "X" }).Should().BeFalse();
        SpotCheckUseCases.Worth(new Item { Kind = Kinds.Cd, Title = "X", PriceHint = 60 }).Should().BeTrue();
        SpotCheckUseCases.Worth(new Item { Kind = Kinds.Dvd, Title = "X" }).Should().BeTrue();
    }

    // 2026-11-25: "Kun skal vurderes" shows exactly the things Martin prices himself, dearest first - not a random sample.
    [Test]
    public async Task Only_assess_lists_the_things_that_need_an_own_price()
    {
        Item Dvd(int id, string title) => new() { Id = id, Kind = Kinds.Dvd, Title = title, Confidence = 1, PricesAt = DateTime.UtcNow };
        static PriceFind Named(PriceFind f, string name) { f.Name = name; return f; }   // a listing must name our title, or it is Forkert by rule
        var dear = Dvd(1, "Dear film");
        var plain = Dvd(2, "Plain film");
        var items = new Mock<IItemRepository>();
        items.Setup(r => r.AllListedAsync(default)).ReturnsAsync([plain, dear]);
        var prices = new Mock<IPriceRepository>();
        prices.Setup(r => r.FindsForAsync(It.IsAny<IReadOnlyCollection<int>>(), default)).ReturnsAsync(new Dictionary<int, List<PriceFind>>
        {
            [1] = [Named(F(1, "Faraos", 400, "a"), "Dear film"), Named(F(1, "Faraos", 420, "b"), "Dear film")],
            [2] = [Named(F(2, "Faraos", 5, "c"), "Plain film"), Named(F(2, "Faraos", 6, "d"), "Plain film")],
        });
        prices.Setup(r => r.VerdictsForAsync(It.IsAny<IReadOnlyCollection<int>>(), default)).ReturnsAsync([]);
        prices.Setup(r => r.ForItemAsync(It.IsAny<int>(), default)).ReturnsAsync((new List<PriceFind>(), new List<PriceCheck>()));

        var rows = await new SpotCheckUseCases(items.Object, prices.Object).SampleAsync(60, null, onlyAssess: true);

        rows.Select(r => r.Item.Id).Should().Equal(1);
    }
}
