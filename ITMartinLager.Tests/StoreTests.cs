using FluentAssertions;
using ITMartinLager.Application;
using ITMartinLager.Domain;
using ITMartinLager.Infrastructure.Persistence;
using ITMartinLager.Infrastructure.PriceSites;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ITMartinLager.Tests;

public class StoreTests
{
    private SqliteConnection _conn = null!;
    private LagerDb Db() => new(new DbContextOptionsBuilder<LagerDb>().UseSqlite(_conn).Options);
    private TestDbFactory Factory => new(_conn);
    private BoxRepository Boxes => new(Factory);
    private ItemRepository Items => new(Factory);
    private RegistrationUseCases Registration => new(Items);

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
        (await Boxes.AddNextAsync("Garagen", default)).Code.Should().Be("K0001");
        (await Boxes.AddNextAsync("", default)).Code.Should().Be("K0002");
    }

    [Test]
    public async Task Same_issue_same_condition_in_same_box_adds_up_but_other_condition_is_its_own_line()
    {
        var box = await Boxes.AddNextAsync("", default);
        await Registration.SaveAsync(box.Id, null, [Aa("12", 1968), Aa("13", 1968)]);
        var (added, merged) = await Registration.SaveAsync(box.Id, null, [Aa("12", 1968, qty: 2), Aa("12", 1968, "Slidt")]);
        using var db = Db();

        added.Should().Be(1);
        merged.Should().Be(1);
        var rows = await db.Items.OrderBy(i => i.Id).ToListAsync();
        rows.Should().HaveCount(3);
        rows[0].Quantity.Should().Be(3);
        rows[2].Condition.Should().Be("Slidt");
    }

    [Test]
    public async Task Without_a_box_a_known_thing_is_found_anywhere_in_stock_and_counted_up()
    {
        (await Registration.SaveAsync(null, null, [Aa("12", 1968)])).Should().Be((1, 0));
        var known = await Registration.InStockAsync([Aa("12", 1968), Aa("13", 1968), Aa("12", 1968, "Slidt")]);
        known.Select(k => k.Quantity).Should().Equal(1, 0, 1);     // another condition is flagged too - the person decides

        // The person said "+1 eksemplar" in Vareinfo: the same row goes up, no new row.
        (await Registration.SaveAsync(null, null, [Aa("12", 1968)])).Should().Be((0, 1));
        (await Items.GetAsync(known[0].ItemId!.Value, default))!.Quantity.Should().Be(2);
        (await Items.GetAsync(known[0].ItemId!.Value, default))!.BoxId.Should().BeNull();
    }

    [Test]
    public async Task A_second_photo_read_a_little_differently_is_still_recognised()
    {
        await Registration.SaveAsync(null, null,
        [
            new(Kinds.Bog, "Start-Up 5-String Banjo", "", "", "", null, "", "Meget god", 1, 1, ""),
            new(Kinds.Tegneserie, "Storm Petersen Album", "", "Storm P.", "29", 1941, "", "God", 1, 1, ""),
        ]);
        var again = await Registration.InStockAsync(
        [
            new(Kinds.Bog, "Start-Up: 5-String Banjo", "", "", "", null, "", "God", 1, 1, ""),              // colon, other condition
            new(Kinds.Tegneserie, "Storm Petersen album", "", "Storm Petersen", "29", null, "", "Slidt", 1, 1, ""),
            new(Kinds.Tegneserie, "Storm Petersen Album", "", "", "30", 1942, "", "God", 1, 1, ""),          // another årgang
        ]);
        again.Select(a => a.Quantity).Should().Equal(1, 1, 0);

        (await Registration.AddCopiesAsync([(again[0].ItemId!.Value, 1)])).Should().Be(1);
        (await Items.GetAsync(again[0].ItemId!.Value, default))!.Quantity.Should().Be(2);
    }

        [Test]
    public void Valued_waiting_or_no_price_is_said_from_the_item()
    {
        new Item().Valuation.Should().Be(Valuations.Waiting);
        new Item { PricesAt = DateTime.UtcNow }.Valuation.Should().Be(Valuations.NoPrice);
        new Item { PricesAt = DateTime.UtcNow, PriceHint = 45 }.Valuation.Should().Be(Valuations.Valued);
    }

    [Test]
    public async Task Search_needs_every_word_and_ignores_case()
    {
        var box = await Boxes.AddNextAsync("", default);
        await Registration.SaveAsync(box.Id, null,
        [
            Aa("12", 1968),
            new(Kinds.Cd, "Goodbye Yellow Brick Road", "Elton John", "", "", 1973, "", "Meget god", 1, 1, ""),
        ]);
        (await Items.SearchAsync("anders 1968", null, null, 100, default)).Count.Should().Be(1);
        (await Items.SearchAsync("ELTON yellow", null, null, 100, default)).Count.Should().Be(1);
        (await Items.SearchAsync("elton 1968", null, null, 100, default)).Count.Should().Be(0);
        (await Items.SearchAsync("", Kinds.Cd, null, 100, default)).Count.Should().Be(1);
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
        AuctionResult.Apply(i, new(Guid.Parse(i.AuctionItemId), "Sold", 150m, "Rico", "12345678"), Now).Should().BeTrue();
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
        AuctionResult.Apply(i, new(Guid.NewGuid(), status, null, null, null), Now).Should().BeTrue();
        i.Status.Should().Be(Statuses.Lager);
        i.AuctionItemId.Should().BeEmpty();
        i.Note.Should().Contain(note);
    }

    [TestCase("Pending")]
    [TestCase("Active")]
    public void Still_running_changes_nothing(string status)
    {
        var i = OnAuction();
        AuctionResult.Apply(i, new(Guid.NewGuid(), status, null, null, null), Now).Should().BeFalse();
        i.Status.Should().Be(Statuses.PaaAuktion);
    }
}
