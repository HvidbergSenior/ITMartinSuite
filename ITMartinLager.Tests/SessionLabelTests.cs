using FluentAssertions;
using ITMartinLager.Application;
using ITMartinLager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ITMartinLager.Tests;

// Boxes (labels removed 2026-11-15 - the session name is written on the box).
public class SessionLabelTests
{
    private SqliteConnection _conn = null!;

    [SetUp]
    public void Open()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        using var db = new LagerDb(new DbContextOptionsBuilder<LagerDb>().UseSqlite(_conn).Options);
        db.Database.EnsureCreated();
    }

    [TearDown]
    public void Close() => _conn.Dispose();

    [Test]
    public async Task An_admin_deletes_a_box_and_what_Vareinfo_sent_for_it()
    {
        var f = new TestDbFactory(_conn);
        var intake = new IntakeRepository(f);
        var boxes = new BoxUseCases(new BoxRepository(f), new ItemRepository(f), intake);
        var rows = new BoxRepository(f);
        await rows.GetOrAddAsync("K0031", default);
        await rows.GetOrAddAsync("K0032", default);
        await intake.AddAsync([new ITMartinLager.Domain.BoxShot { VareinfoId = 1, BoxCode = "K0031", Helper = "Martin", TakenAt = DateTime.UtcNow }], [], default);

        (await boxes.DeleteAsync("K0031")).Should().Be(0);

        (await boxes.AllAsync()).Select(b => b.Code).Should().Equal("K0032");
        (await intake.PerBoxAsync(default)).Should().NotContain(b => b.BoxCode == "K0031");
        (await boxes.DeleteAsync("K9876")).Should().Be(0);   // not there: nothing happens
    }
}
