using FluentAssertions;
using ITMartinVareinfo.Application;
using ITMartinVareinfo.Domain;
using ITMartinVareinfo.Infrastructure.Persistence;
using ITMartinVarer.Contracts;
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
        _clock.Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);   // one fixture instance: a test that moves the day must not move the next
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        using (var db = Factory.CreateDbContext()) VareinfoDbInitializer.Upgrade(db);
        _sessions = new SessionUseCases(new SessionRepository(Factory), new PhotoRepository(Factory), new ScanRepository(Factory), _clock);
        _capture = new CaptureUseCases(new PhotoRepository(Factory), new ScanRepository(Factory), Mock.Of<IPhotoStore>(),
            new SessionRepository(Factory), _sessions, _clock);
    }

    [TearDown]
    public void Close() => _conn.Dispose();

    // One press (2026-11-10): a button joins today's session of that button, or makes it. Mine varer only since 2026-11-15.
    [Test]
    public async Task The_same_button_the_same_day_gives_everyone_the_same_session()
    {
        var mine = await _sessions.TodayAsync(_martin, "Blade og album");
        var hers = await _sessions.TodayAsync(_bertil, "Blade og album");   // a helper, own phone - no "start a session first"
        hers.Id.Should().Be(mine.Id);
        mine.Title.Should().Be("Blade og album – 20. oktober");
        mine.Kind.Should().Be("Blade og album");
        mine.Fund.Should().BeFalse();
    }

    [Test]
    public async Task Each_button_is_its_own_session_and_a_new_day_starts_new_ones_and_closes_the_old()
    {
        var blade = await _sessions.TodayAsync(_martin, "Blade og album");
        var books = await _sessions.TodayAsync(_martin, "Bøger");
        new[] { blade.Id, books.Id }.Should().OnlyHaveUniqueItems();
        _clock.Now = _clock.Now.AddDays(1);
        (await _sessions.TodayAsync(_martin, "Blade og album")).Id.Should().NotBe(blade.Id);
        (await _sessions.OpenForAsync(blade.Id, _martin)).Should().BeNull();
        (await _sessions.OpenForAsync(books.Id, _martin)).Should().BeNull();
    }

    // An old Tjek session (before Tjek got its own app) is never joined by a Mine varer button.
    [Test]
    public async Task An_old_Tjek_session_is_not_reused_for_Mine_varer()
    {
        var repo = new SessionRepository(Factory);
        var old = WorkSession.Start(1, "Tjek: Bøger – 20. oktober", _clock.Now.UtcDateTime, "Bøger");
        old.Fund = true;
        await repo.SaveAsync(old, default);
        var mine = await _sessions.TodayAsync(_martin, "Bøger");
        mine.Id.Should().NotBe(old.Id);
        mine.Fund.Should().BeFalse();
    }

    [Test]
    public async Task An_unknown_button_is_Blandet_and_a_day_session_goes_to_Lager_to_be_read_at_once()
    {
        var s = await _sessions.TodayAsync(_martin, "noget andet");
        s.Kind.Should().Be("Blandet");
        var boxes = await _sessions.BoxesAsync([s.BoxCode]);
        boxes.Single().Should().Be(new CapturedBox(s.BoxCode, "20. oktober", false, "Blandet", true));
    }

    [Test]
    public async Task The_overview_shows_photos_scans_and_helpers_per_session()
    {
        var s = await _sessions.TodayAsync(_martin, "Film");
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
