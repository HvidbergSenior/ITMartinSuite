using FluentAssertions;
using ITMartinLager.Server.Data;
using ITMartinLager.Server.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ITMartinLager.Tests;

public class StoreTests
{
    private SqliteConnection _conn = null!;
    private LagerDb Db() => new(new DbContextOptionsBuilder<LagerDb>().UseSqlite(_conn).Options);

    [SetUp]
    public void Open()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        using var db = Db();
        db.Database.EnsureCreated();
    }

    [TearDown]
    public void Close() => _conn.Dispose();

    private static Found Aa(string nr, int year, string cond = "God", int qty = 1) =>
        new(Kinds.AndersAnd, "Anders And & Co.", "", "Anders And & Co.", nr, year, "", cond, qty, 0.9, "");

    [Test]
    public async Task Boxes_are_numbered_K0001_K0002()
    {
        using var db = Db();
        (await Store.NewBoxAsync(db, "Garagen")).Code.Should().Be("K0001");
        (await Store.NewBoxAsync(db, "")).Code.Should().Be("K0002");
    }

    [Test]
    public async Task Same_issue_same_condition_in_same_box_adds_up_but_other_condition_is_its_own_line()
    {
        using var db = Db();
        var box = await Store.NewBoxAsync(db, "");
        await Store.SaveAsync(db, box.Id, null, [Aa("12", 1968), Aa("13", 1968)]);
        var (added, merged) = await Store.SaveAsync(db, box.Id, null, [Aa("12", 1968, qty: 2), Aa("12", 1968, "Slidt")]);

        added.Should().Be(1);
        merged.Should().Be(1);
        var rows = await db.Items.OrderBy(i => i.Id).ToListAsync();
        rows.Should().HaveCount(3);
        rows[0].Quantity.Should().Be(3);
        rows[2].Condition.Should().Be("Slidt");
    }

    [Test]
    public async Task Search_needs_every_word_and_ignores_case()
    {
        using var db = Db();
        var box = await Store.NewBoxAsync(db, "");
        await Store.SaveAsync(db, box.Id, null,
        [
            Aa("12", 1968),
            new(Kinds.Cd, "Goodbye Yellow Brick Road", "Elton John", "", "", 1973, "", "Meget god", 1, 1, ""),
        ]);
        (await Store.Search(db.Items, "anders 1968", null).CountAsync()).Should().Be(1);
        (await Store.Search(db.Items, "ELTON yellow", null).CountAsync()).Should().Be(1);
        (await Store.Search(db.Items, "elton 1968", null).CountAsync()).Should().Be(0);
        (await Store.Search(db.Items, "", Kinds.Cd).CountAsync()).Should().Be(1);
    }

    [Test]
    public void Ai_answers_are_cleaned_unknown_kinds_conditions_and_silly_years()
    {
        var f = PileReader.Clean(new PileReader.RawItem { Kind = "jumbobog", Title = " Jumbobog 50 ", Condition = "fin", Year = 19, Quantity = 0, Confidence = 3 });
        f.Kind.Should().Be(Kinds.Jumbobog);
        f.Title.Should().Be("Jumbobog 50");
        f.Condition.Should().Be("God");
        f.Year.Should().BeNull();
        f.Quantity.Should().Be(1);
        f.Confidence.Should().Be(1);
        PileReader.Clean(new PileReader.RawItem { Kind = "vinyl", Title = "x" }).Kind.Should().Be(Kinds.Andet);
    }
}

public class AuctionApplyTests
{
    private static Item OnAuction() => new() { Title = "Fantomet", Status = Statuses.PaaAuktion, AuctionItemId = Guid.NewGuid().ToString(), AuctionCode = "ABC123" };
    private static readonly DateTime Now = new(2026, 10, 5, 20, 0, 0);

    [Test]
    public void Sold_marks_the_item_sold_with_price_and_buyer_and_keeps_the_auction_id()
    {
        var i = OnAuction();
        AuctionLink.Apply(i, new(Guid.Parse(i.AuctionItemId), "Sold", 150m, "Rico", "12345678"), Now).Should().BeTrue();
        i.Status.Should().Be(Statuses.Solgt);
        i.SoldPrice.Should().Be(150m);
        i.SoldTo.Should().Be("Rico, 12345678");
        i.SoldAt.Should().Be(Now);
        i.Note.Should().Contain("Solgt på auktion ABC123").And.Contain("150 kr");
        i.AuctionItemId.Should().NotBeEmpty();
    }

    [TestCase("Passed", "Ikke solgt på auktion ABC123")]
    [TestCase("Gone", "Fjernet fra auktion ABC123")]
    public void Not_sold_or_removed_puts_it_back_on_the_shelf(string status, string note)
    {
        var i = OnAuction();
        AuctionLink.Apply(i, new(Guid.NewGuid(), status, null, null, null), Now).Should().BeTrue();
        i.Status.Should().Be(Statuses.Lager);
        i.AuctionItemId.Should().BeEmpty();
        i.Note.Should().Contain(note);
    }

    [TestCase("Pending")]
    [TestCase("Active")]
    public void Still_running_changes_nothing(string status)
    {
        var i = OnAuction();
        AuctionLink.Apply(i, new(Guid.NewGuid(), status, null, null, null), Now).Should().BeFalse();
        i.Status.Should().Be(Statuses.PaaAuktion);
    }
}
