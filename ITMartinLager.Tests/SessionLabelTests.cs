using FluentAssertions;
using ITMartinLager.Application;
using ITMartinLager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ITMartinLager.Tests;

// The label sheet for a Vareinfo session (2026-10-26: "after a session with 20 boxes ... an easy way to make qr labels").
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
    public async Task The_session_boxes_are_made_with_their_names_and_test_boxes_never_get_a_label()
    {
        var f = new TestDbFactory(_conn);
        var boxes = new BoxUseCases(new BoxRepository(f), new ItemRepository(f));

        var sheet = await boxes.LabelSheetAsync("K0021~Anders And 1970'erne, K0022, K9999~Test, nonsense");

        sheet.Select(b => (b.Code, b.Note)).Should().Equal(("K0021", "Anders And 1970'erne"), ("K0022", ""));
        (await boxes.AllAsync()).Single(b => b.Code == "K0021").Note.Should().Be("Anders And 1970'erne");
        // Printed again with another name: the name already on the box is kept.
        (await boxes.LabelSheetAsync("K0021~Noget andet")).Single().Note.Should().Be("Anders And 1970'erne");
    }

    [Test]
    public async Task An_admin_deletes_a_box_and_what_Vareinfo_sent_for_it()
    {
        var f = new TestDbFactory(_conn);
        var intake = new IntakeRepository(f);
        var boxes = new BoxUseCases(new BoxRepository(f), new ItemRepository(f), intake);
        await boxes.LabelSheetAsync("K0031~Slettes, K0032");
        await intake.AddAsync([new ITMartinLager.Domain.BoxShot { VareinfoId = 1, BoxCode = "K0031", Helper = "Martin", TakenAt = DateTime.UtcNow }], [], default);

        (await boxes.DeleteAsync("K0031")).Should().Be(0);

        (await boxes.AllAsync()).Select(b => b.Code).Should().Equal("K0032");
        (await intake.PerBoxAsync(default)).Should().NotContain(b => b.BoxCode == "K0031");
        (await boxes.DeleteAsync("K9876")).Should().Be(0);   // not there: nothing happens
    }
}
