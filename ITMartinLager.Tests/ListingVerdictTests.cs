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
}
