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

    [SetUp]
    public void Open()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        using var db = Factory.CreateDbContext();
        VareinfoDbInitializer.Upgrade(db);
    }

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
        new(new BoxClaimRepository(Factory), new PhotoRepository(Factory), new ScanRepository(Factory), store ?? Store().Object, _clock);

    [Test]
    public async Task One_helper_per_box_and_a_finished_or_forgotten_box_is_free_again()
    {
        var capture = Capture();
        (await capture.TakeBoxAsync(7, _rico)).BoxCode.Should().Be("K0007");

        await capture.Invoking(c => c.TakeBoxAsync(7, _anna)).Should().ThrowAsync<VareinfoException>().WithMessage("Kasse 7 bruges af Rico*");
        (await capture.TakeBoxAsync(7, _rico)).HelperName.Should().Be("Rico");   // Rico again (reload) is fine
        (await capture.TakeBoxAsync(8, _anna)).HelperName.Should().Be("Anna");

        await capture.FinishBoxAsync("K0007", _rico);
        (await capture.MyBoxAsync(_rico)).Should().BeNull();
        (await capture.TakeBoxAsync(7, _anna)).HelperName.Should().Be("Anna");   // finished = free

        _clock.Now += BoxClaim.Idle + TimeSpan.FromMinutes(1);                   // Anna went home without "færdig"
        (await capture.TakeBoxAsync(8, _rico)).HelperName.Should().Be("Rico");
        await capture.Invoking(c => c.TakeBoxAsync(0, _rico)).Should().ThrowAsync<VareinfoException>();
    }

    [Test]
    public async Task Hundred_helpers_taking_boxes_at_the_same_moment_each_get_their_own()
    {
        var capture = Capture();
        var helpers = Enumerable.Range(1, 100).Select(n => new Taker(n, $"Hjælper {n}")).ToList();

        // Everybody asks for box 1 at once: exactly one gets it.
        var tries = await Task.WhenAll(helpers.Select(async h =>
        {
            try { await capture.TakeBoxAsync(1, h); return true; } catch (VareinfoException) { return false; }
        }));
        tries.Count(ok => ok).Should().Be(1);

        // Each their own box: all 100 at once, all fine.
        await Task.WhenAll(helpers.Select(h => capture.TakeBoxAsync(100 + h.Id, h)));
        (await capture.OverviewAsync()).Count(b => b.Busy).Should().Be(101);
    }

    [Test]
    public async Task An_upload_is_kept_at_once_and_made_ready_in_the_background()
    {
        var store = Store();
        var capture = Capture(store.Object);
        await capture.TakeBoxAsync(7, _rico);
        await capture.StoreAsync(new MemoryStream([1]), "K0007", _rico);
        await capture.StoreAsync(new MemoryStream([1]), "K0007", _rico);

        store.Verify(s => s.SaveRawAsync(It.Is<string>(p => p.StartsWith("inbox")), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        store.Verify(s => s.ShrinkAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);   // nothing cut while uploading
        (await capture.NewForLagerAsync(10)).Photos.Should().BeEmpty();                                        // not ready yet
        (await capture.CountAsync("K0007")).Should().Be((2, 0));

        (await capture.ProcessWaitingAsync(10, 4)).Should().Be(2);
        store.Verify(s => s.Delete(It.Is<string>(p => p.StartsWith("inbox"))), Times.Exactly(2));             // the upload is not kept
        var batch = await capture.NewForLagerAsync(10);
        batch.Photos.Should().HaveCount(2).And.OnlyContain(p => p.BoxCode == "K0007" && p.Helper == "Rico" && p.Tiles == 4);
        (await capture.PhotoFileAsync(batch.Photos[0].Id, 3)).Should().EndWith("-t3.jpg");
        (await capture.PhotoFileAsync(batch.Photos[0].Id, 4)).Should().BeNull();
    }

    [Test]
    public async Task A_photo_that_cannot_be_opened_is_given_up_after_three_tries()
    {
        var store = Store();
        store.Setup(x => x.ShrinkAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidDataException("broken"));
        var capture = Capture(store.Object);
        await capture.StoreAsync(new MemoryStream([1]), "K0007", _rico);

        for (var n = 0; n < Photo.MaxAttempts; n++) await capture.ProcessWaitingAsync(10, 1);

        (await capture.ProcessWaitingAsync(10, 1)).Should().Be(0);
        var batch = await capture.NewForLagerAsync(10);
        batch.Photos.Single().Tiles.Should().Be(0);
        (await capture.PhotoFileAsync(batch.Photos[0].Id, null)).Should().BeNull();   // Lager sees it as gone
    }

    [Test]
    public async Task Lager_gets_each_photo_and_scan_until_it_says_it_has_them()
    {
        var capture = Capture();
        await capture.StoreAsync(new MemoryStream([1]), "K0001", _rico);
        await capture.ProcessWaitingAsync(10, 1);
        (await capture.ScanAsync("9780552131063", "K0001", _rico)).Should().Be(1);
        (await capture.ScanAsync("9780552131063", "K0001", _rico)).Should().Be(2);   // same code again = one more copy
        await capture.Invoking(c => c.ScanAsync("12", "K0001", _rico)).Should().ThrowAsync<VareinfoException>();

        var first = await capture.NewForLagerAsync(10);
        (await capture.NewForLagerAsync(10)).Should().BeEquivalentTo(first);         // asked again before saying OK: the same
        first.Scans.Single().Quantity.Should().Be(2);

        await capture.ReceivedAsync(new CaptureReceived(first.Photos.Select(p => p.Id).ToList(), first.Scans.Select(s => s.Id).ToList()));
        var after = await capture.NewForLagerAsync(10);
        after.Photos.Should().BeEmpty();
        after.Scans.Should().BeEmpty();
        (await capture.ScanAsync("9780552131063", "K0001", _rico)).Should().Be(1);   // Lager has the first two - a new row
    }

    [Test]
    public async Task A_photo_still_on_the_phone_lands_in_its_own_box_even_after_the_box_is_finished()
    {
        var capture = Capture();
        await capture.TakeBoxAsync(7, _rico);
        await capture.FinishBoxAsync("K0007", _rico);
        await capture.TakeBoxAsync(7, _anna);

        await capture.StoreAsync(new MemoryStream([1]), "K0007", _rico);
        await capture.Invoking(c => c.StoreAsync(new MemoryStream([1]), "", _rico)).Should().ThrowAsync<VareinfoException>();

        (await capture.CountAsync("K0007")).Photos.Should().Be(1);
        (await capture.MyBoxAsync(_anna))!.BoxCode.Should().Be("K0007");
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

// The box label QR (Lager prints it, Vareinfo scans it) - 2026-10-21.
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
