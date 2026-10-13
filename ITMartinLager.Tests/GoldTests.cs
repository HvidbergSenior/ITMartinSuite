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
    public void Bundles_decades_whole_years_reprints_and_books_are_not_gold()
    {
        // Real DBA titles that were on the list on 2026-10-13.
        var picked = Gold.Pick(AndersAnd,
        [
            O("Anders And og Co. 1961, 48 numre", 550),
            O("Anders And og Co. 1980'erne, 155 blade", 250),
            O("Anders And & Co tegneserier 1988 – 52 hæfter i alt", 200),
            O("3 dtk. Anders And & Co. tegneserier 1953", 500),
            O("Anders And & Co 1960’erne tegneseriehæfter bundt", 300),
            O("Anders And 1950’ rene", 160),
            O("Anders And & Co tegneserier årgang 1986", 250),
            O("Anders And årgang 1953 + nr. 1 fra 1954. Indbundet.", 310),
            O("Anders And & Co. Genoptryk af nr. 1 marts 1949", 150),
            O("Anders And bog – En daglig dosis (Disney, 1990)", 140),
            O("Tegneserier, Anders And nr 7 marts 1956 8. Årgang", 1000),
            O("Anders And & Co tegneseriehæfte nr 9 fra 1958 årgang 10", 250),
        ], "DBA", DateTime.UtcNow);
        picked.Select(p => (p.Number, p.Year)).Should().Equal(("7", 1956), ("9", 1958));
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
        Gold.Interesting(list, Kinds.AndersAnd, "Anders And & Co.", "", "5", 1949)!.Should().StartWith("🔎 MÅSKE").And.Contain("fra 1949");   // only the year: 52 issues
        Gold.Interesting(list, Kinds.AndersAnd, "Anders And & Co.", "", "17", 1973)!.Should().StartWith("🥈 SØLV");
        Gold.Interesting(list, Kinds.AndersAnd, "Anders And & Co.", "", "5", 1980)!.Should().StartWith("🔎 MÅSKE").And.Contain("1949-1973");
        Gold.Interesting(list, Kinds.AndersAnd, "Anders And & Co.", "", "5", 1973)!.Should().StartWith("🔎 MÅSKE");   // 1973 but nr. 17 is the dear one
        Gold.Interesting(list, Kinds.Bog, "Mort", "", "", 1987).Should().BeNull();
        Gold.Interesting(list, Kinds.Bog, "Mort", "", "", 1987, ownPrice: 250)!.Should().StartWith("🥈 SØLV");
    }

    [Test]
    public void Bidders_see_the_badge_and_the_price_elsewhere()
    {
        var list = Gold.Pick(AndersAnd, [O("Anders And & Co. nr. 17 1973", 1500)], "DBA", DateTime.UtcNow);
        Gold.ForAuction(list, Kinds.AndersAnd, "Anders And & Co.", "", "17", 1973).Should().Be("🏆 GULD – sælges for ca. 1500 kr andre steder");
        Gold.ForAuction(list, Kinds.Bog, "Mort", "", "", 1987, 260).Should().Be("🥈 SØLV – sælges for ca. 260 kr andre steder");
        Gold.ForAuction(list, Kinds.AndersAnd, "Anders And & Co.", "", "3", 1990)!.Should().StartWith("🔎 MÅSKE");
        Gold.ForAuction(list, Kinds.Bog, "Mort", "", "", 1987, 40).Should().BeNull();
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
