using FluentAssertions;
using ITMartinSalg.Application;
using ITMartinSalg.Domain;
using ITMartinSalg.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace ITMartinSalg.Tests;

// "Interessant" on the marketplace (2026-10-26): one mark per customer per thing, pressing again takes it back, the
// shop shows the count, and Salg's Interesse page lists the most marked first.
public class InterestTests
{
    private SqliteConnection _conn = null!;
    private TestDbFactory Factory => new(_conn);
    private FakeStock _stock = null!;
    private InterestUseCases _interest = null!;

    [SetUp]
    public void Open()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        using var db = Factory.CreateDbContext();
        SalgDbInitializer.Upgrade(db);
        _stock = new FakeStock();
        _interest = new InterestUseCases(new InterestRepository(Factory), _stock, new SalgOptions(), TimeProvider.System);
    }

    [TearDown]
    public void Close() => _conn.Dispose();

    [Test]
    public async Task Pressing_marks_and_pressing_again_takes_it_back()
    {
        var tintin = _stock.Add(Kinds.Tegneserie, "Tintin 5", price: 80);

        (await _interest.ToggleAsync("4821 7730", tintin.Id)).Should().Be(new ShopInterest(true, 1));
        (await _interest.ToggleAsync("1111-2222", tintin.Id)).Should().Be(new ShopInterest(true, 2));
        (await _interest.MineAsync("48217730")).Should().Equal(tintin.Id);

        (await _interest.ToggleAsync("48217730", tintin.Id)).Should().Be(new ShopInterest(false, 1));
        (await _interest.MineAsync("48217730")).Should().BeEmpty();
    }

    [Test]
    public async Task No_mark_without_a_kundenummer_or_on_a_thing_that_is_gone()
    {
        var tintin = _stock.Add(Kinds.Tegneserie, "Tintin 5", price: 80);
        (await _interest.ToggleAsync("123", tintin.Id)).Error.Should().Be("Log ind med dit kundenummer først.");
        (await _interest.ToggleAsync("48217730", 999)).Error.Should().Be("Varen er her ikke mere.");
        (await _interest.TopAsync()).Should().BeEmpty();
    }

    [Test]
    public async Task The_list_has_the_most_marked_first_and_says_when_a_thing_is_sold()
    {
        var a = _stock.Add(Kinds.Bog, "Mort", price: 40);
        var b = _stock.Add(Kinds.Tegneserie, "Anders And 1972 nr. 3", price: 60);
        await _interest.ToggleAsync("11111111", a.Id);
        await _interest.ToggleAsync("11111111", b.Id);
        await _interest.ToggleAsync("22222222", b.Id);
        _stock.Items.Remove(a);

        var top = await _interest.TopAsync();
        top.Select(r => (r.ItemId, r.Count, r.ForSale)).Should().Equal((b.Id, 2, true), (a.Id, 1, false));
        top[0].BuyNowKr.Should().Be(60);
    }

    [Test]
    public void A_kundenummer_is_8_digits_typed_any_way() =>
        (Kundenummer.Normalise(" 4821-7730 "), Kundenummer.Normalise("4821773"), Kundenummer.Normalise(null)).Should().Be(("48217730", null, null));
}
