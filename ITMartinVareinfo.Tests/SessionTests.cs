using FluentAssertions;
using ITMartinVareinfo.Application;
using ITMartinVareinfo.Domain;
using ITMartinVareinfo.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Moq;

namespace ITMartinVareinfo.Tests;

// Sessions (user 2026-10-29: "it should be Session 1, 2, 3, etc... A session should be named something like location
// and/or intent" - "so remove all that has to do with boxes").
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
        _sessions = new SessionUseCases(new SessionRepository(Factory), new PhotoRepository(Factory), new ScanRepository(Factory), _clock);
        _capture = new CaptureUseCases(new PhotoRepository(Factory), new ScanRepository(Factory), Mock.Of<IPhotoStore>(),
            new SessionRepository(Factory), _sessions, _clock);
    }

    [TearDown]
    public void Close() => _conn.Dispose();

    [Test]
    public async Task Sessions_are_numbered_1_2_3_and_titled_by_place_and_purpose()
    {
        var one = await _sessions.StartAsync(" Boxit, reol 2 ");
        var two = await _sessions.StartAsync(null);
        await _sessions.PracticeAsync(_bertil);   // a practice session takes no number
        var three = await _sessions.StartAsync("Loppemarked lørdag");

        (one.Number, two.Number, three.Number).Should().Be((1, 2, 3));
        one.Title.Should().Be("Session 1 – Boxit, reol 2");
        two.Title.Should().Be("Session 2");
        three.BoxCode.Should().Be("K5003");   // Lager's code, never shown here

        await _sessions.RenameAsync(two.Id, "Garagen, hylde 1");
        (await _sessions.AllAsync()).Select(s => s.Title).Should().Equal("Session 3 – Loppemarked lørdag", "Session 2 – Garagen, hylde 1", "Session 1 – Boxit, reol 2");
    }

    [Test]
    public async Task Helpers_see_the_open_sessions_and_a_closed_one_leaves_the_list()
    {
        (await _sessions.ChoicesAsync(_bertil)).Should().BeEmpty();
        var s = await _sessions.StartAsync("Boxit, reol 2");
        await _capture.StoreAsync(new MemoryStream([1]), s.Id, _vibeke);

        (await _sessions.ChoicesAsync(_bertil)).Should().Equal(new SessionChoice(s.Id, "Session 1 – Boxit, reol 2", 1));
        (await _sessions.OpenForAsync(s.Id, _bertil)).Should().NotBeNull();

        await _sessions.CloseAsync(s.Id);
        (await _sessions.ChoicesAsync(_bertil)).Should().BeEmpty();
        (await _sessions.OpenForAsync(s.Id, _bertil)).Should().BeNull();
        await _sessions.ReopenAsync(s.Id);
        (await _sessions.ChoicesAsync(_bertil)).Should().HaveCount(1);
    }

    [Test]
    public async Task A_session_given_to_a_helper_is_only_theirs_and_the_others_see_the_rest()
    {
        var a = await _sessions.StartAsync("Hos Marianne");
        var b = await _sessions.StartAsync("Boxit");
        await _sessions.GiveAsync(a.Id, new HelperChoice(_bertil.Id, "Bertil"));

        (await _sessions.ChoicesAsync(_bertil)).Select(c => c.Id).Should().Equal(a.Id);   // only his own
        (await _sessions.ChoicesAsync(_vibeke)).Select(c => c.Id).Should().Equal(b.Id);   // the ones for everybody
        (await _sessions.ChoicesAsync(_martin)).Should().HaveCount(2);                     // Martin sees all

        await _sessions.GiveAsync(a.Id, null);   // back to Alle
        (await _sessions.ChoicesAsync(_vibeke)).Should().HaveCount(2);
    }

    [Test]
    public async Task Everyone_has_their_own_practice_session_the_same_every_time()
    {
        var bertils = await _sessions.PracticeAsync(_bertil);
        var vibekes = await _sessions.PracticeAsync(_vibeke);
        (bertils.BoxCode, vibekes.BoxCode).Should().Be(("K9999", "K9998"));
        bertils.Title.Should().Be("Øvesession – Bertil");

        _clock.Now = _clock.Now.AddDays(3);
        (await _sessions.PracticeAsync(_bertil)).Id.Should().Be(bertils.Id);
        (await _sessions.OpenForAsync(bertils.Id, _vibeke)).Should().BeNull();
        (await _sessions.OpenForAsync(bertils.Id, _martin)).Should().BeNull();   // not even Martin's
        (await _sessions.ChoicesAsync(_martin)).Should().BeEmpty();
        (await _sessions.AllAsync()).Should().BeEmpty();
    }

    [Test]
    public async Task Only_a_session_without_photos_can_be_deleted()
    {
        var empty = await _sessions.StartAsync("Prøve");
        var used = await _sessions.StartAsync("Boxit");
        await _capture.StoreAsync(new MemoryStream([1]), used.Id, _bertil);

        await _sessions.DeleteAsync(empty.Id);
        await _sessions.Invoking(x => x.DeleteAsync(used.Id)).Should().ThrowAsync<VareinfoException>().WithMessage("*luk den i stedet*");
        (await _sessions.AllAsync()).Select(s => s.Id).Should().Equal(used.Id);
    }

    [Test]
    public async Task The_overview_shows_photos_scans_and_helpers_per_session()
    {
        var s = await _sessions.StartAsync("Boxit");
        await _capture.StoreAsync(new MemoryStream([1]), s.Id, _bertil);
        await _capture.StoreAsync(new MemoryStream([1]), s.Id, _vibeke);
        await _capture.ScanAsync("9780552131063", s.Id, _martin);

        var st = (await _sessions.OverviewAsync()).Single();
        (st.Photos, st.Scans).Should().Be((2, 1));
        st.Helpers.Should().Equal("Bertil", "Vibeke");
    }
}

public class UpgradeTests
{
    [Test]
    public void A_database_with_the_box_sessions_of_2026_10_26_gets_the_session_columns_and_keeps_its_tables()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE "Sessions" ("Id" INTEGER PRIMARY KEY AUTOINCREMENT, "Name" TEXT NOT NULL, "CreatedAt" TEXT NOT NULL, "ClosedAt" TEXT NULL);
                CREATE TABLE "SessionBoxes" ("Id" INTEGER PRIMARY KEY AUTOINCREMENT, "SessionId" INTEGER NOT NULL, "BoxCode" TEXT NOT NULL);
                CREATE TABLE "Photos" ("Id" INTEGER PRIMARY KEY AUTOINCREMENT, "BoxCode" TEXT NOT NULL, "HelperId" INTEGER NOT NULL, "Helper" TEXT NOT NULL,
                    "RawPath" TEXT NOT NULL, "Path" TEXT NOT NULL, "Tiles" INTEGER NOT NULL, "Ready" INTEGER NOT NULL, "Attempts" INTEGER NOT NULL,
                    "TakenAt" TEXT NOT NULL, "SentAt" TEXT NULL);
                """;
            cmd.ExecuteNonQuery();
        }
        var f = new TestDbFactory(conn);
        using (var db = f.CreateDbContext()) VareinfoDbInitializer.Upgrade(db);
        using (var db = f.CreateDbContext()) VareinfoDbInitializer.Upgrade(db);   // a second start changes nothing

        using (var db = f.CreateDbContext())
        {
            db.Sessions.Add(WorkSession.Start(1, "Boxit", DateTime.UtcNow));
            db.SaveChanges();
            db.Photos.Add(new Photo { SessionId = 1, BoxCode = "K5001", RawPath = "inbox/a.jpg" });
            db.SaveChanges();
        }
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name = 'SessionBoxes'";
        Convert.ToInt32(check.ExecuteScalar()).Should().Be(1);
    }
}
