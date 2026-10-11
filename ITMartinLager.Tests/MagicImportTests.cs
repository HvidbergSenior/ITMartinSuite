using FluentAssertions;
using Moq;
using ITMartinLager.Application;
using ITMartinLager.Domain;
using ITMartinLager.Infrastructure.External;
using ITMartinLager.Infrastructure.Persistence;
using ITMartinLager.Infrastructure.PriceSites;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ITMartinLager.Tests;

public class MagicImportTests
{
    private SqliteConnection _conn = null!;
    private LagerDb Db() => new(new DbContextOptionsBuilder<LagerDb>().UseSqlite(_conn).Options);

    private MagicImportUseCases Import(params MagicCardInfo[] cards)
    {
        var gateway = new Mock<IMagicGateway>();
        gateway.Setup(g => g.CardsAsync("Bogshoppen", default)).ReturnsAsync(cards.ToList());
        var f = new TestDbFactory(_conn);
        return new MagicImportUseCases(gateway.Object, new ItemRepository(f), new BoxRepository(f));
    }

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
        MagicCards.Kr(1.00m).Should().Be(7);
        MagicCards.Kr(0.05m).Should().Be(1);
        MagicCards.Kr(null).Should().BeNull();
        MagicCards.Kr(0m).Should().BeNull();
    }

    [Test]
    public async Task Fetching_again_updates_instead_of_duplicating_and_keeps_a_typed_price()
    {
        var id = Guid.NewGuid();
        var (added, updated, box) = await Import(Card(id, 2, 1.5m)).ImportAsync("Bogshoppen", 0);
        (added, updated).Should().Be((1, 0));
        box.Code.Should().Be("K0001");
        using var db = Db();
        var item = await db.Items.SingleAsync();
        item.Kind.Should().Be(Kinds.Magic);
        item.Series.Should().Be("Magic 2010");
        item.PriceHint.Should().Be(11);
        item.Interest.Should().Contain("Lightning Bolt").And.Contain("11 kr");
        item.Price = 25;
        await db.SaveChangesAsync();

        // Fetched again with the default "new box" choice: same row, same box, no new empty box.
        var again = await Import(Card(id, 4, 2m)).ImportAsync("Bogshoppen", 0);
        (again.Added, again.Updated, again.Box.Code).Should().Be((0, 1, "K0001"));
        using var db2 = Db();
        item = await db2.Items.SingleAsync();
        item.Quantity.Should().Be(4);
        item.PriceHint.Should().Be(15);
        item.Price.Should().Be(25);
    }
}
