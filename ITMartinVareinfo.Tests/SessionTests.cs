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

    // One press (2026-11-10): Mine varer / Tjek + a button join today's session of that button, or make it.
    [Test]
    public async Task The_same_button_the_same_day_gives_everyone_the_same_session()
    {
        var mine = await _sessions.TodayAsync(_martin, fund: false, "Blade og album");
        var hers = await _sessions.TodayAsync(_bertil, fund: false, "Blade og album");   // a helper, own phone - no "start a session first"
        hers.Id.Should().Be(mine.Id);
        mine.Title.Should().Be("Blade og album – 20. oktober");
        mine.Kind.Should().Be("Blade og album");
        mine.Fund.Should().BeFalse();
    }

    [Test]
    public async Task Each_button_and_Tjek_are_their_own_sessions_and_a_new_day_starts_new_ones()
    {
        var blade = await _sessions.TodayAsync(_martin, fund: false, "Blade og album");
        var books = await _sessions.TodayAsync(_martin, fund: false, "Bøger");
        var check = await _sessions.TodayAsync(_bertil, fund: true, "Blade og album");
        new[] { blade.Id, books.Id, check.Id }.Should().OnlyHaveUniqueItems();
        check.Fund.Should().BeTrue();
        check.Title.Should().Be("Tjek: Blade og album – 20. oktober");
        _clock.Now = _clock.Now.AddDays(1);
        (await _sessions.TodayAsync(_martin, fund: false, "Blade og album")).Id.Should().NotBe(blade.Id);
    }

    // Tjek answers (2026-11-10): only a Tjek session asks Lager, with its own box code; another session gets nothing.
    [Test]
    public async Task Only_a_Tjek_session_gets_answers_and_presses_go_to_its_own_box()
    {
        var lager = new Mock<ICheckAnswers>();
        lager.Setup(l => l.ForAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CheckAnswers([], [], 0));
        lager.Setup(l => l.PressAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var check = new CheckUseCases(lager.Object, _sessions, _capture);
        var tjek = await _sessions.TodayAsync(_bertil, fund: true, "Blandet");
        var mine = await _sessions.TodayAsync(_bertil, fund: false, "Blandet");

        (await check.AnswersAsync(tjek.Id, _bertil)).Should().NotBeNull();
        (await check.AnswersAsync(mine.Id, _bertil)).Should().BeNull("Mine varer has no answers to pick from");
        (await check.PressAsync(tjek.Id, 7, CheckUseCases.Take, _bertil)).Should().BeNull();
        lager.Verify(l => l.PressAsync(tjek.BoxCode, 7, "tog", It.IsAny<CancellationToken>()), Times.Once);
        (await check.PressAsync(mine.Id, 7, CheckUseCases.Take, _bertil)).Should().NotBeNull();
        (await check.PressAsync(tjek.Id, 7, "slet", _bertil)).Should().Be("Ukendt knap.");
        lager.Verify(l => l.ForAsync(mine.BoxCode, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Yesterdays_day_sessions_close_by_themselves_and_are_not_on_the_list()
    {
        var blade = await _sessions.TodayAsync(_martin, fund: false, "Blade og album");
        var handMade = await _sessions.StartAsync("Boxit, reol 2");
        (await _sessions.ChoicesAsync(_martin)).Select(c => c.Id).Should().Equal(handMade.Id);   // today's button session is reached by its button
        _clock.Now = _clock.Now.AddDays(1);
        await _sessions.TodayAsync(_martin, fund: false, "Bøger");
        (await _sessions.OpenForAsync(blade.Id, _martin)).Should().BeNull();
        (await _sessions.OpenForAsync(handMade.Id, _martin)).Should().NotBeNull();   // Martin's own sessions stay open
    }

    [Test]
    public async Task An_unknown_button_is_Blandet_and_a_day_session_goes_to_Lager_to_be_read_at_once()
    {
        var s = await _sessions.TodayAsync(_martin, fund: false, "noget andet");
        s.Kind.Should().Be("Blandet");
        var boxes = await _sessions.BoxesAsync([s.BoxCode]);
        boxes.Single().Should().Be(new CapturedBox(s.BoxCode, "Blandet – 20. oktober", false, "Blandet", true));
        var old = await _sessions.StartAsync("Boxit");
        (await _sessions.BoxesAsync([old.BoxCode])).Single().ReadNow.Should().BeFalse();
    }

    [Test]
    public async Task A_closed_session_is_not_reused_today()
    {
        var first = await _sessions.TodayAsync(_martin, fund: false, "Film");
        await _sessions.CloseAsync(first.Id);
        (await _sessions.TodayAsync(_martin, fund: false, "Film")).Id.Should().NotBe(first.Id);
    }

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
    public async Task A_Fund_session_goes_to_Lager_as_Fund()
    {
        var ours = await _sessions.StartAsync("Garagen");
        var fund = await _sessions.StartAsync("Genbrugsplads Lystrup", fund: true);
        (await _sessions.BoxesAsync([ours.BoxCode, fund.BoxCode])).Should().BeEquivalentTo(
            [new CapturedBox(ours.BoxCode, "Session 1 – Garagen"), new CapturedBox(fund.BoxCode, "Session 2 – Genbrugsplads Lystrup", true)]);
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
