using FluentAssertions;
using ITMartinLager.Domain;

namespace ITMartinLager.Tests;

// The gold list and the Danish market first (user 2026-10-12). Listing names are real DBA titles from that day.
public class GoldTests
{
    private static readonly GoldCategory AndersAnd = Gold.Categories.First(c => c.Name == "Anders And & Co.");
    private static readonly GoldCategory Jumbo = Gold.Categories.First(c => c.Name == "Jumbobøger");
    private static Offer O(string name, decimal kr) => new(name, kr, "https://dba.dk/" + kr);

    [Test]
    public void Only_single_issues_become_gold_not_collections_or_dream_prices()
    {
        var picked = Gold.Pick(AndersAnd,
        [
            O("anders and og co, tegneserie", 123456789),
            O("Stor samling Anders And & Co og Walt Disney tegneserier", 99999),
            O("Anders And & Co. 1983.  komplet 1-52.", 175),
            O("Anders And & Co Originalt blad fra marts 1949", 6000),
            O("Walt Disney Anders And & Co. tegneserie nr. 35 30. august 1966", 1000),
            O("Anders And & Co. ( 36 stk ). Ekstra", 100),
        ], "DBA", DateTime.UtcNow);
        picked.Select(p => (p.Number, p.Year, p.Kr)).Should().Equal(("", 1949, 6000), ("35", 1966, 1000));
    }

    [Test]
    public void Watches_coins_posters_and_figures_are_not_gold()
    {
        var tintin = Gold.Categories.First(c => c.Name == "Tintin");
        Gold.Pick(tintin,
        [
            O("Omega - Geneve Chronostop TinTin - 146.012 - Mænd - 1966", 9711),
            O("Tintin - vintage dansk plakat fra 1983", 5995),
            O("Opslagstavle egetræsramme - Tintin - 1993", 2350),
            O("Tintin og Picaroerne – 1. udgave, 1. oplag (1976, dansk)", 5000),
        ], "DBA", DateTime.UtcNow).Should().ContainSingle().Which.Year.Should().Be(1976);
    }

    [Test]
    public void Gold_silver_and_bronze_are_by_price()
    {
        (Gold.TierOf(3600), Gold.TierOf(400), Gold.TierOf(150)).Should().Be(("Guld", "Sølv", "Bronze"));
        Gold.Pick(AndersAnd, [O("Anders And & Co. nr. 3 1960", 90)], "DBA", DateTime.UtcNow).Should().BeEmpty();   // under bronze
        Gold.Pick(AndersAnd, [O("Anders And & Co. nr. 3 1960", 250)], "DBA", DateTime.UtcNow).Single().Badge.Should().Be("🥈 SØLV");
    }

    [Test]
    public void Every_thing_gets_a_one_line_answer_worth_looking_into_or_not()
    {
        var list = Gold.Pick(AndersAnd, [O("Anders And & Co Originalt blad fra marts 1949", 3600), O("Anders And & Co. nr. 17 1973", 400)], "DBA", DateTime.UtcNow);
        Gold.Interesting(list, Kinds.AndersAnd, "Anders And & Co.", "", "5", 1949)!.Should().StartWith("🔎 Tjek nærmere").And.Contain("fra 1949");   // only the year: 52 issues
        Gold.Interesting(list, Kinds.AndersAnd, "Anders And & Co.", "", "17", 1973)!.Should().StartWith("🥈 SØLV");
        Gold.Interesting(list, Kinds.AndersAnd, "Anders And & Co.", "", "5", 1980)!.Should().StartWith("🔎 Tjek nærmere").And.Contain("1949-1973");
        Gold.Interesting(list, Kinds.AndersAnd, "Anders And & Co.", "", "5", 1973)!.Should().StartWith("🔎 Tjek nærmere");   // 1973 but nr. 17 is the dear one
        Gold.Interesting(list, Kinds.Bog, "Mort", "", "", 1987).Should().BeNull();
        Gold.Interesting(list, Kinds.Bog, "Mort", "", "", 1987, ownPrice: 250)!.Should().StartWith("🥈 SØLV");
    }

    [Test]
    public void A_Jumbobog_needs_its_number()
    {
        var picked = Gold.Pick(Jumbo, [O("ANDERS AND JUMBOBOG", 2150), O("Jumbobog nr. 1 - Anders And i topform", 900)], "DBA", DateTime.UtcNow);
        picked.Should().ContainSingle().Which.Number.Should().Be("1");
    }

    [Test]
    public void A_registered_issue_on_the_list_is_gold()
    {
        var list = Gold.Pick(AndersAnd, [O("Walt Disney Anders And & Co. tegneserie nr. 35 30. august 1966", 1000)], "DBA", DateTime.UtcNow);
        Gold.Match(list, Kinds.AndersAnd, "Anders And & Co.", "Anders And & Co.", "35", 1966).Should().NotBeNull();
        Gold.Match(list, Kinds.AndersAnd, "Anders And & Co.", "", "35", 1967).Should().BeNull();
        Gold.Match(list, Kinds.Tegneserie, "Tintin", "", "35", 1966).Should().BeNull();
    }

    [Test]
    public void Our_price_comes_from_the_Danish_market_when_it_has_enough()
    {
        Pricing.NormalDanishFirst([("DBA", 40), ("DBA", 60), ("AbeBooks", 400), ("eBay", 500), ("ZVAB", 450)]).Should().Be(60);
        Pricing.NormalDanishFirst([("DBA", 40), ("AbeBooks", 400), ("eBay", 500)]).Should().Be(400);   // too few Danish: all count
    }
}
