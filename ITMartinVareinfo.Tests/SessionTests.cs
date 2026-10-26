using FluentAssertions;
using ITMartinVareinfo.Application;
using ITMartinVareinfo.Domain;
using ITMartinVareinfo.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Moq;

namespace ITMartinVareinfo.Tests;

// Sessions (user 2026-10-26: "I prepare a session with 4 helpers. I point to 20 boxes and name them" - "a helper should
// ONLY have their Testkasse. I prepare which boxes so that they just have to choose").
public class SessionTests
{
    private SqliteConnection _conn = null!;
    private TestDbFactory Factory => new(_conn);
    private readonly TestClock _clock = new();
    private readonly Taker _martin = new(0, "Martin"), _bertil = new(-11112222, "Bertil"), _vibeke = new(-33334444, "Vibeke");
    private SessionUseCases _sessions = null!;
    private CaptureUseCases _capture = null!;

    [SetUp]
    public void Open()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        using (var db = Factory.CreateDbContext()) VareinfoDbInitializer.Upgrade(db);
        _sessions = new SessionUseCases(new SessionRepository(Factory), new BoxClaimRepository(Factory), new PhotoRepository(Factory), _clock);
        _capture = new CaptureUseCases(new BoxClaimRepository(Factory), new PhotoRepository(Factory), new ScanRepository(Factory),
            Mock.Of<IPhotoStore>(), _sessions, _clock);
    }

    [TearDown]
    public void Close() => _conn.Dispose();

    [Test]
    public async Task New_boxes_get_the_next_numbers_and_never_one_already_used()
    {
        await _capture.TakeBoxAsync(5, _martin);   // Martin started box 5 by hand earlier
        var s = await _sessions.StartAsync("Lørdag i garagen");
        await _sessions.AddBoxAsync(s.Id, 2, "Anders And 1970'erne");

        (await _sessions.NewBoxesAsync(s.Id, 3)).Should().Equal(6, 7, 8);
        var again = await _sessions.StartAsync(null);
        (await _sessions.NewBoxesAsync(again.Id, 2)).Should().Equal(9, 10);
        await _sessions.Invoking(x => x.AddBoxAsync(s.Id, 2, null)).Should().ThrowAsync<VareinfoException>().WithMessage("Kasse 2 er allerede med.");
    }

    [Test]
    public async Task A_helper_sees_only_the_prepared_boxes_and_cannot_take_another()
    {
        (await _sessions.ChoicesAsync(_bertil)).Should().BeEmpty();   // nothing ready: only the test box
        await _capture.Invoking(c => c.TakeBoxAsync(3, _bertil)).Should().ThrowAsync<VareinfoException>().WithMessage("*ikke gjort klar*");
        (await _capture.MyTestBoxAsync(_bertil)).IsTest.Should().BeTrue();

        var s = await _sessions.StartAsync("Lørdag");
        await _sessions.AddBoxAsync(s.Id, 3, "Bøger");
        await _sessions.AddBoxAsync(s.Id, 4, null);
        (await _sessions.ChoicesAsync(_bertil)).Select(c => (c.Title, c.State)).Should().Equal(("Kasse 3 – Bøger", ChoiceState.Free), ("Kasse 4", ChoiceState.Free));

        await _capture.TakeBoxAsync(3, _bertil);
        (await _sessions.ChoicesAsync(_vibeke)).First().Should().Match<BoxChoice>(c => c.State == ChoiceState.Busy && c.Who == "Bertil" && !c.CanTake);
        (await _sessions.ChoicesAsync(_bertil)).First().State.Should().Be(ChoiceState.Mine);

        await _capture.FinishBoxAsync("K0003", _bertil);
        (await _sessions.ChoicesAsync(_vibeke)).First().State.Should().Be(ChoiceState.Done);

        await _sessions.CloseAsync(s.Id);
        (await _sessions.ChoicesAsync(_vibeke)).Should().BeEmpty();
        await _capture.Invoking(c => c.TakeBoxAsync(4, _vibeke)).Should().ThrowAsync<VareinfoException>();
        (await _capture.TakeBoxAsync(4, _martin)).HelperName.Should().Be("Martin");   // Martin may still take any box
    }

    [Test]
    public async Task A_box_given_to_a_helper_is_only_theirs_and_the_others_see_the_pool()
    {
        var s = await _sessions.StartAsync("Hos Marianne og Jørgen");
        await _sessions.AddBoxAsync(s.Id, 1, "Martin");
        await _sessions.AddBoxAsync(s.Id, 2, "Marianne");
        await _sessions.AddBoxAsync(s.Id, 3, null);
        var boxes = (await _sessions.AllAsync()).Single().Boxes.OrderBy(b => b.SortOrder).ToList();
        await _sessions.GiveBoxAsync(boxes[1].Id, new HelperChoice(_bertil.Id, "Bertil"));

        (await _sessions.ChoicesAsync(_bertil)).Select(c => c.BoxCode).Should().Equal("K0002");          // only his own
        (await _sessions.ChoicesAsync(_vibeke)).Select(c => c.BoxCode).Should().Equal("K0001", "K0003"); // the pool
        (await _sessions.ChoicesAsync(_martin)).Should().HaveCount(3);                                   // Martin sees all
        await _capture.Invoking(c => c.TakeBoxAsync(2, _vibeke)).Should().ThrowAsync<VareinfoException>();
        (await _capture.TakeBoxAsync(2, _bertil)).HelperName.Should().Be("Bertil");

        await _sessions.GiveBoxAsync(boxes[1].Id, null);   // back to Alle
        (await _sessions.ChoicesAsync(_vibeke)).Should().HaveCount(3);
    }

    [Test]
    public async Task Deleting_a_session_removes_its_list_but_not_the_photos()
    {
        var s = await _sessions.StartAsync("Prøve");
        await _sessions.AddBoxAsync(s.Id, 5, null);
        await _sessions.DeleteAsync(s.Id);
        (await _sessions.AllAsync()).Should().BeEmpty();
        (await _sessions.ChoicesAsync(_bertil)).Should().BeEmpty();
    }

    [Test]
    public async Task The_box_names_go_to_Lager_with_the_photos()
    {
        var s = await _sessions.StartAsync("Lørdag");
        await _sessions.AddBoxAsync(s.Id, 3, "Bøger");
        await _sessions.AddBoxAsync(s.Id, 4, null);
        (await _sessions.NamesAsync(["K0003", "K0004", "K0009"])).Should().BeEquivalentTo(new Dictionary<string, string> { ["K0003"] = "Bøger" });
    }

    [Test]
    public void A_session_without_a_name_is_named_after_the_day() =>
        WorkSession.Start(" ", new DateTime(2026, 10, 31, 9, 0, 0, DateTimeKind.Utc)).Name.Should().StartWith("Session 31.");
}

public class UpgradeTests
{
    [Test]
    public void A_database_from_before_the_sessions_gets_the_new_tables()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        var f = new TestDbFactory(conn);
        using (var db = f.CreateDbContext()) VareinfoDbInitializer.Upgrade(db);
        using (var cmd = conn.CreateCommand()) { cmd.CommandText = "DROP TABLE SessionBoxes; DROP TABLE Sessions;"; cmd.ExecuteNonQuery(); }

        using (var db = f.CreateDbContext()) VareinfoDbInitializer.Upgrade(db);

        using var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name IN ('Sessions', 'SessionBoxes')";
        Convert.ToInt32(check.ExecuteScalar()).Should().Be(2);
    }
}
