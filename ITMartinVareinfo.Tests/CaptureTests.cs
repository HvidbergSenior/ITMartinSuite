using FluentAssertions;
using ITMartinVareinfo.Application;
using ITMartinVareinfo.Domain;
using ITMartinVareinfo.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ITMartinVareinfo.Tests;

public sealed class TestDbFactory(SqliteConnection conn) : IDbContextFactory<VareinfoDb>
{
    public VareinfoDb CreateDbContext() => new(new DbContextOptionsBuilder<VareinfoDb>().UseSqlite(conn).Options);
}

/// <summary>A clock the test moves by hand.</summary>
public sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}

public class CaptureTests
{
    private SqliteConnection _conn = null!;
    private TestDbFactory Factory => new(_conn);
    private readonly TestClock _clock = new();
    private readonly Taker _rico = new(1, "Rico"), _anna = new(2, "Anna");
    private WorkSession _session = null!;

    [SetUp]
    public async Task Open()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        using (var db = Factory.CreateDbContext()) VareinfoDbInitializer.Upgrade(db);
        _session = await Sessions().StartAsync("Boxit, reol 2");
    }

    private SessionUseCases Sessions() =>
        new(new SessionRepository(Factory), new PhotoRepository(Factory), new ScanRepository(Factory), _clock);

    [TearDown]
    public void Close() => _conn.Dispose();

    private static Mock<IPhotoStore> Store()
    {
        var p = new Mock<IPhotoStore>();
        p.Setup(x => x.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([9]);
        p.Setup(x => x.ShrinkAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).ReturnsAsync([1]);
        p.Setup(x => x.TilesAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).ReturnsAsync([[2], [3], [4], [5]]);
        p.Setup(x => x.FullPath(It.IsAny<string>())).Returns((string path) => "/data/" + path);
        return p;
    }

    private CaptureUseCases Capture(IPhotoStore? store = null) =>
        new(new PhotoRepository(Factory), new ScanRepository(Factory), store ?? Store().Object, new SessionRepository(Factory), Sessions(), _clock);

    [Test]
    public async Task Hundred_helpers_in_the_same_session_at_the_same_moment_all_get_their_photos_kept()
    {
        var capture = Capture();
        var helpers = Enumerable.Range(1, 100).Select(n => new Taker(n, $"Hjælper {n}")).ToList();

        await Task.WhenAll(helpers.Select(h => capture.StoreAsync(new MemoryStream([1]), _session.Id, h)));

        (await capture.CountAsync(_session.Id)).Photos.Should().Be(100);
        var status = (await Sessions().OverviewAsync()).Single();
        status.Helpers.Should().HaveCount(100);
        status.LastAt.Should().Be(_clock.Now.UtcDateTime);
    }

    [Test]
    public async Task An_upload_is_kept_at_once_and_made_ready_in_the_background()
    {
        var store = Store();
        var capture = Capture(store.Object);
        await capture.StoreAsync(new MemoryStream([1]), _session.Id, _rico);
        await capture.StoreAsync(new MemoryStream([1]), _session.Id, _rico);

        store.Verify(s => s.SaveRawAsync(It.Is<string>(p => p.StartsWith("inbox")), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        store.Verify(s => s.ShrinkAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);   // nothing cut while uploading
        (await capture.NewForLagerAsync(10)).Photos.Should().BeEmpty();                                        // not ready yet
        (await capture.CountAsync(_session.Id)).Should().Be((2, 0));

        (await capture.ProcessWaitingAsync(10, 4)).Should().Be(2);
        store.Verify(s => s.Delete(It.Is<string>(p => p.StartsWith("inbox"))), Times.Exactly(2));             // the upload is not kept
        var batch = await capture.NewForLagerAsync(10);
        batch.Photos.Should().HaveCount(2).And.OnlyContain(p => p.BoxCode == "K5001" && p.Helper == "Rico" && p.Tiles == 4);
        (await capture.PhotoFileAsync(batch.Photos[0].Id, 3)).Should().EndWith("-t3.jpg");
        (await capture.PhotoFileAsync(batch.Photos[0].Id, 4)).Should().BeNull();
    }

    [Test]
    public async Task A_photo_that_cannot_be_opened_is_given_up_after_three_tries()
    {
        var store = Store();
        store.Setup(x => x.ShrinkAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidDataException("broken"));
        var capture = Capture(store.Object);
        await capture.StoreAsync(new MemoryStream([1]), _session.Id, _rico);

        for (var n = 0; n < Photo.MaxAttempts; n++) await capture.ProcessWaitingAsync(10, 1);

        (await capture.ProcessWaitingAsync(10, 1)).Should().Be(0);
        var batch = await capture.NewForLagerAsync(10);
        batch.Photos.Single().Tiles.Should().Be(0);
        (await capture.PhotoFileAsync(batch.Photos[0].Id, null)).Should().BeNull();   // Lager sees it as gone
    }

    [Test]
    public async Task Lager_gets_each_photo_and_scan_with_the_session_title_until_it_says_it_has_them()
    {
        var capture = Capture();
        await capture.StoreAsync(new MemoryStream([1]), _session.Id, _rico);
        await capture.ProcessWaitingAsync(10, 1);
        (await capture.ScanAsync("9780552131063", _session.Id, _rico)).Should().Be(1);
        (await capture.ScanAsync("9780552131063", _session.Id, _rico)).Should().Be(2);   // same code again = one more copy
        await capture.Invoking(c => c.ScanAsync("12", _session.Id, _rico)).Should().ThrowAsync<VareinfoException>();

        var first = await capture.NewForLagerAsync(10);
        (await capture.NewForLagerAsync(10)).Should().BeEquivalentTo(first);         // asked again before saying OK: the same
        first.Scans.Single().Quantity.Should().Be(2);
        first.Boxes.Should().Equal(new CapturedBox("K5001", "Session 1 – Boxit, reol 2"));

        await capture.ReceivedAsync(new CaptureReceived(first.Photos.Select(p => p.Id).ToList(), first.Scans.Select(s => s.Id).ToList()));
        var after = await capture.NewForLagerAsync(10);
        after.Photos.Should().BeEmpty();
        after.Scans.Should().BeEmpty();
        (await capture.ScanAsync("9780552131063", _session.Id, _rico)).Should().Be(1);   // Lager has the first two - a new row
    }

    // 2026-10-29: Vibeke's first photos waited on her phone while the session was deleted - and were thrown away as "afvist".
    // A gone session is its own answer, so the phone keeps the photo and asks for a session instead.
    [Test]
    public async Task A_photo_for_a_deleted_session_is_told_apart_from_a_bad_photo()
    {
        var gone = await Sessions().StartAsync("Slettes");
        await Sessions().DeleteAsync(gone.Id);

        await Capture().Invoking(c => c.StoreAsync(new MemoryStream([1]), gone.Id, _rico)).Should().ThrowAsync<SessionGoneException>();
    }

    [Test]
    public async Task A_photo_still_on_the_phone_lands_in_its_session_even_after_it_was_closed()
    {
        var capture = Capture();
        await Sessions().CloseAsync(_session.Id);

        await capture.StoreAsync(new MemoryStream([1]), _session.Id, _rico);
        await capture.Invoking(c => c.StoreAsync(new MemoryStream([1]), 0, _rico)).Should().ThrowAsync<SessionGoneException>();

        (await capture.CountAsync(_session.Id)).Photos.Should().Be(1);
    }

    [Test]
    public async Task Nobody_uploads_into_a_session_that_is_not_for_them()
    {
        var sessions = Sessions();
        await sessions.GiveAsync(_session.Id, new HelperChoice(_anna.Id, "Anna"));
        var ricos = await sessions.PracticeAsync(_rico);

        await Capture().Invoking(c => c.StoreAsync(new MemoryStream([1]), _session.Id, _rico)).Should().ThrowAsync<SessionGoneException>();
        await Capture().Invoking(c => c.StoreAsync(new MemoryStream([1]), ricos.Id, _anna)).Should().ThrowAsync<SessionGoneException>();
        await Capture().StoreAsync(new MemoryStream([1]), _session.Id, _anna);
    }

    private sealed class OneHelper : IHelperNumbers
    {
        public Task<string?> HelperNameAsync(string kundenummer, CancellationToken ct = default) =>
            Task.FromResult(kundenummer == "48217730" ? "Jonas" : null);
    }

    [Test]
    public async Task A_helper_ticked_in_Kunder_signs_in_with_the_kundenummer()
    {
        var helpers = new HelperUseCases(new HelperRepository(Factory), new VareinfoOptions { AdminPin = "246810" }, new OneHelper());
        var me = await helpers.SignInAsync("4821 7730");
        me.Should().Be(new Taker(-48217730, "Jonas"));
        me!.IsAdmin.Should().BeFalse();
        (await helpers.SignInAsync("11112222")).Should().BeNull();   // a kundenummer that is not a helper
    }

    private sealed class HelperAndAdmin : IHelperNumbers
    {
        public Task<string?> HelperNameAsync(string kundenummer, CancellationToken ct = default) =>
            Task.FromResult(kundenummer == "48217730" ? "Martin" : null);
        public Task<string?> AdminNameAsync(string kundenummer, CancellationToken ct = default) =>
            Task.FromResult(kundenummer == "55556666" ? "MartinAdmin" : null);
    }

    [Test]
    public async Task An_admin_ticked_in_Kunder_signs_in_as_admin_and_a_helper_as_a_helper()
    {
        var helpers = new HelperUseCases(new HelperRepository(Factory), new VareinfoOptions { AdminPin = "246810" }, new HelperAndAdmin());
        (await helpers.SignInAsync("5555 6666")).Should().Be(new Taker(0, "MartinAdmin"));
        (await helpers.SignInAsync("48217730"))!.IsAdmin.Should().BeFalse();
    }

    [Test]
    public async Task Helpers_sign_in_with_their_own_code_and_a_removed_code_stops_working()
    {
        var helpers = new HelperUseCases(new HelperRepository(Factory), new VareinfoOptions { AdminPin = "246810" });
        var rico = await helpers.AddAsync(" Rico ");
        rico.Code.Should().MatchRegex("^[0-9]{6}$");
        rico.Name.Should().Be("Rico");

        (await helpers.SignInAsync(rico.Code)).Should().Be(new Taker(rico.Id, "Rico"));
        (await helpers.SignInAsync("246810")).Should().Be(new Taker(0, "Martin"));
        (await helpers.SignInAsync("246810"))!.IsAdmin.Should().BeTrue();
        (await helpers.SignInAsync("000")).Should().BeNull();
        await helpers.Invoking(h => h.AddAsync("  ")).Should().ThrowAsync<VareinfoException>();

        await helpers.RemoveAsync(rico.Id);
        (await helpers.SignInAsync(rico.Code)).Should().BeNull();
        (await helpers.AllAsync()).Should().BeEmpty();
    }

    [Test]
    public void The_reading_app_s_old_tables_are_kept_aside_and_the_capture_tables_are_made()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE "Photos" ("Id" INTEGER PRIMARY KEY, "Path" TEXT NOT NULL, "BoxId" INTEGER NULL, "Found" INTEGER NOT NULL,
                    "TakenAt" TEXT NOT NULL, "Waiting" INTEGER NOT NULL DEFAULT 0, "ReadWanted" INTEGER NOT NULL DEFAULT 0);
                INSERT INTO "Photos" ("Id", "Path", "Found", "TakenAt") VALUES (5, 'photos/a.jpg', 8, '2026-10-12 10:00:00');
                CREATE TABLE "KolofonShots" ("Id" INTEGER PRIMARY KEY);
                """;
            cmd.ExecuteNonQuery();
        }
        using var db = new TestDbFactory(conn).CreateDbContext();
        VareinfoDbInitializer.Upgrade(db);
        VareinfoDbInitializer.Upgrade(db);   // a second start changes nothing

        db.Photos.Should().BeEmpty();
        db.Helpers.Should().BeEmpty();
        using var check = conn.CreateCommand();
        check.CommandText = """SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name LIKE 'Old_Photos_%'""";
        Convert.ToInt32(check.ExecuteScalar()).Should().Be(1);
    }
}

// Domain knows nothing outside itself except the shared words of the buying/selling apps (ITMartinVarer.Contracts).
public class LayerTests
{
    private const string SharedWords = "ITMartinVarer.Contracts";

    private static string[] Refs(Type t) => t.Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();

    [Test]
    public void Domain_has_no_outside_dependencies() =>
        Refs(typeof(Photo)).Where(n => n.StartsWith("ITMartin") || n.StartsWith("Microsoft.EntityFramework") || n.StartsWith("Anthropic"))
            .Should().BeEquivalentTo([SharedWords]);

    [Test]
    public void Application_depends_only_on_Domain() =>
        Refs(typeof(CaptureUseCases)).Where(n => n.StartsWith("ITMartin") || n.StartsWith("Microsoft.EntityFramework") || n.StartsWith("Anthropic"))
            .Should().BeEquivalentTo(["ITMartinVareinfo.Domain", SharedWords]);

    [Test]
    public void No_page_uses_the_database_or_infrastructure()
    {
        var pages = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "ITMartinVareinfo.Server", "Pages");
        foreach (var file in Directory.GetFiles(pages, "*.razor"))
        {
            var text = File.ReadAllText(file);
            text.Should().NotContain("DbContext", file).And.NotContain("VareinfoDb", file).And.NotContain("Infrastructure", file);
        }
    }

    // The capture app never calls out - no AI, no Lager (2026-10-20): Lager fetches from here.
    [Test]
    public void Vareinfo_calls_no_AI_and_no_Lager() =>
        new[] { typeof(Photo), typeof(CaptureUseCases), typeof(Infrastructure.Persistence.VareinfoDb) }.SelectMany(Refs)
            .Should().NotContain(n => n.StartsWith("ITMartinLager") || n.StartsWith("Anthropic"));
}

// The box label QR (Lager prints it) - 2026-10-21. Vareinfo no longer scans it (sessions, 2026-10-29); the words stay
// in ITMartinVarer.Contracts until Lager gets sessions too.
public class BoxLabelTests
{
    [TestCase("https://lager.bogshoppen.dk/kasse/K0007", 7)]
    [TestCase("http://10.0.0.200:9149/kasse/K0123/", 123)]
    [TestCase("K0001", 1)]
    [TestCase("k42", 42)]
    [TestCase("https://example.com/vare/5", 0)]
    [TestCase("5706710230823", 0)]
    [TestCase("", 0)]
    public void The_box_number_is_read_from_the_label(string scanned, int number) =>
        BoxCodes.FromLabel(scanned).Should().Be(number);

    [Test]
    public void A_sheet_is_in_order_and_capped()
    {
        BoxCodes.Range(0, 3).Should().Equal("K0001", "K0002", "K0003");
        BoxCodes.Range(5, 2).Should().Equal("K0005");
        BoxCodes.Range(1, 500).Should().HaveCount(BoxCodes.MaxSheet);
    }
}
