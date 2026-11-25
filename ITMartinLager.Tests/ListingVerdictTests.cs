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

    // 2026-11-25 (user: "put them in a category so I can check if I want"): a thing is Gennemgået when every listing has a mark.
    [Test]
    public void A_thing_is_reviewed_when_every_listing_has_a_mark()
    {
        var a = F(1, "Faraos", 100, "a"); var b = F(1, "Faraos", 120, "b");
        var item = new Item { Id = 1, Title = "x" };
        var marks = new Dictionary<string, string> { [ListingVerdict.KeyOf(a)] = SpotCheckUseCases.Rigtig };
        SpotCheckUseCases.Reviewed(new SpotCheckUseCases.Row(item, [a, b], marks, 3)).Should().BeFalse();
        marks[ListingVerdict.KeyOf(b)] = SpotCheckUseCases.KanIkkeSe;
        SpotCheckUseCases.Reviewed(new SpotCheckUseCases.Row(item, [a, b], marks, 3)).Should().BeTrue();
        SpotCheckUseCases.Reviewed(new SpotCheckUseCases.Row(item, [], [], 3)).Should().BeFalse();
    }

    // 2026-11-25: Stikprøve showed listings the rules already throw out; they need no answer and do not count in the source table.
    [Test]
    public void A_listing_for_another_season_is_excluded_by_the_rules()
    {
        var house = new Item { Id = 1, Kind = Kinds.Dvd, Title = "House – Season Three" };
        FixedPrice.CountsByName(house, "House M.D. sæson 3 DVD-boks").Should().BeTrue();
        FixedPrice.CountsByName(house, "House MD sæson 1 DVD-boks fra Universal").Should().BeFalse();
    }

    [Test]
    public void Excluded_listings_need_no_mark_to_make_a_thing_reviewed()
    {
        var item = new Item { Id = 1, Kind = Kinds.Dvd, Title = "House – Season Three" };
        var right = new PriceFind { ItemId = 1, Source = "Faraos", Kr = 50, Url = "a", Name = "House sæson 3" };
        var other = new PriceFind { ItemId = 1, Source = "GulogGratis", Kr = 32, Url = "b", Name = "House MD sæson 1 DVD-boks" };
        var marks = new Dictionary<string, string> { [ListingVerdict.KeyOf(right)] = SpotCheckUseCases.Rigtig };
        SpotCheckUseCases.Reviewed(new SpotCheckUseCases.Row(item, [right, other], marks, 3)).Should().BeTrue();
    }

    [Test]
    public void AbeBooks_and_Accord_no_longer_set_our_price()
    {
        FixedPrice.Sources.Should().NotContain(["AbeBooks", "Accord"]);
        FixedPrice.Sources.Should().Contain(["Faraos", "GulogGratis", "Nemos Bibliotek", "Discogs"]);
    }
}
