using FluentAssertions;
using ITMartinLager.Server.Data;
using ITMartinLager.Server.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ITMartinLager.Tests;

public class MagicImportTests
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

    private static MagicCardInfo Card(Guid id, int qty, decimal? eur) =>
        new(id, "Lightning Bolt", "m10", "Magic 2010", 2009, "146", qty, eur, null, null);

    [Test]
    public void Euro_becomes_whole_kroner_and_never_zero()
    {
        MagicLink.Kr(1.00m).Should().Be(7);
        MagicLink.Kr(0.05m).Should().Be(1);
        MagicLink.Kr(null).Should().BeNull();
        MagicLink.Kr(0m).Should().BeNull();
    }

    [Test]
    public async Task Fetching_again_updates_instead_of_duplicating_and_keeps_a_typed_price()
    {
        using var db = Db();
        var box = await Store.NewBoxAsync(db, "Magic");
        var id = Guid.NewGuid();
        (await MagicLink.ImportAsync(db, box.Id, [Card(id, 2, 1.5m)])).Should().Be((1, 0));
        var item = await db.Items.SingleAsync();
        item.Kind.Should().Be(Kinds.Magic);
        item.Series.Should().Be("Magic 2010");
        item.PriceHint.Should().Be(11);
        item.Interest.Should().Contain("Lightning Bolt").And.Contain("11 kr");
        item.Price = 25;
        await db.SaveChangesAsync();

        (await MagicLink.ImportAsync(db, box.Id, [Card(id, 4, 2m)])).Should().Be((0, 1));
        item = await db.Items.SingleAsync();
        item.Quantity.Should().Be(4);
        item.PriceHint.Should().Be(15);
        item.Price.Should().Be(25);
    }
}
