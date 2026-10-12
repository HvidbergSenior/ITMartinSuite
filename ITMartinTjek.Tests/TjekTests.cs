using System.Text.Json;
using FluentAssertions;
using ITMartinTjek.Application;
using ITMartinTjek.Domain;
using ITMartinTjek.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ITMartinTjek.Tests;

public class TjekTests
{
    private const string Key = "ABCD2345EF";

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Factory(DbContextOptions<TjekDbContext> o) : IDbContextFactory<TjekDbContext>
    {
        public TjekDbContext CreateDbContext() => new(o);
    }

    private SqliteConnection _conn = null!;
    private EfSnapshotStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        var o = new DbContextOptionsBuilder<TjekDbContext>().UseSqlite(_conn).Options;
        using (var db = new TjekDbContext(o)) db.Database.EnsureCreated();
        _store = new EfSnapshotStore(new Factory(o));
    }

    [TearDown]
    public void TearDown() => _conn.Dispose();

    private static readonly List<Check> Checks =
    [
        new("net", "Net", "ok", "20 ms", ""), new("mic", "Mikrofon", "warn", "svag", "tal tættere på"),
        new("cam", "Kamera", "bad", "afvist", ""), new("ua", "Browser", "info", "Chrome", ""),
    ];

    [Test]
    public void Rules_count_statuses_and_read_both_kinds_of_saved_run()
    {
        var run = CheckRules.BrowserRun(" Stuen ", Key, Checks);
        (run.Ok, run.Warn, run.Bad, run.Summary, run.Device).Should().Be((1, 1, 1, "1 ok, 1 gule, 1 røde", "Stuen"));
        CheckRules.Parse(run.Json).Should().BeEquivalentTo(Checks);
        CheckRules.Parse("""{"device":"pc","checks":[{"id":"usb","name":"USB","status":"bad","value":"fejl","hint":""}]}""")
            .Should().ContainSingle().Which.Status.Should().Be("bad");
        CheckRules.Parse("ikke json").Should().BeEmpty();
        CheckRules.OwnerKey(" abcd2345ef ").Should().Be(Key);
        CheckRules.OwnerKey("kort").Should().BeNull();
    }

    [Test]
    public void Device_tests_turn_numbers_into_plain_Danish()
    {
        DeviceTests.Mic(true, null, "Headset", 1, 48000).Status.Should().Be("bad");
        DeviceTests.Mic(true, null, "Headset", 10, 48000).Status.Should().Be("warn");
        DeviceTests.Mic(true, null, "Headset", 40, 48000).Value.Should().Be("Headset: top 40 %, 48000 Hz");
        DeviceTests.Camera(true, null, "Webcam", 1280, 720, 30).Value.Should().Be("Webcam: 1280×720 @ 30 fps");
        DeviceTests.Tone(true, null, false).Value.Should().Contain("brugte standard");
    }

    [Test]
    public async Task Runs_and_history_are_only_ever_this_browsers_own()
    {
        var runs = new Runs(_store);
        var history = new History(_store);
        (await runs.SaveBrowserRunAsync("Stuen", Key, Checks)).Should().BeTrue();
        (await runs.SaveBrowserRunAsync("Stuen", "", Checks)).Should().BeFalse("no code, nothing saved");
        (await runs.SaveBrowserRunAsync("Stuen", "OTHERKEY99", Checks)).Should().BeTrue();

        (await history.DevicesAsync(Key)).Should().Equal("Stuen");
        var mine = await history.RunsAsync(Key, "Stuen");
        mine.Should().ContainSingle();
        (await history.RunsAsync("", "Stuen")).Should().BeEmpty();

        await history.DeleteAsync("OTHERKEY99", mine[0].Id);
        (await history.RunsAsync(Key, "Stuen")).Should().ContainSingle("another browser cannot delete my run");
        await history.DeleteAsync(Key, mine[0].Id);
        (await history.RunsAsync(Key, "Stuen")).Should().BeEmpty();
    }

    [Test]
    public async Task Deep_script_run_is_received_and_found_again()
    {
        var runs = new Runs(_store);
        using var bad = JsonDocument.Parse("""{"device":"pc","checks":[]}""");
        await runs.Invoking(r => r.ReceiveDeepRunAsync(bad.RootElement)).Should().ThrowAsync<ArgumentException>().WithMessage("*Koden*");

        using var doc = JsonDocument.Parse("""{"device":"Kontor-pc","key":"abcd2345ef","checks":[{"status":"ok"},{"status":"bad"},{"status":"bad"}]}""");
        var receipt = await runs.ReceiveDeepRunAsync(doc.RootElement);
        (receipt.Ok, receipt.Bad).Should().Be((1, 2));
        receipt.Message.Should().Contain("Kontor-pc");

        var deep = await runs.LatestDeepAsync(Key, "Kontor-pc");
        deep!.Checks.Should().HaveCount(3);
        (await runs.LatestDeepAsync("OTHERKEY99", "Kontor-pc")).Should().BeNull();
    }

    private sealed class FakeAdvisor : IAdvisor
    {
        public int Calls;
        public bool Enabled => true;
        public Task<Advice> AskAsync(string device, string browserJson, string? deepJson, string description, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new Advice($"{device}: ok", [], [], [], []));
        }
    }

    [Test]
    public async Task Advice_stops_at_the_days_budget_and_starts_again_tomorrow()
    {
        var clock = new Clock();
        var advisor = new FakeAdvisor();
        var diagnosis = new Diagnosis(advisor, new AiBudget(2, clock, NullLogger<AiBudget>.Instance));
        (await diagnosis.AskAsync("", Checks, null, "")).Diagnose.Should().Be("ukendt: ok");
        await diagnosis.AskAsync("pc", Checks, null, "");
        await diagnosis.Invoking(d => d.AskAsync("pc", Checks, null, "")).Should().ThrowAsync<AiBudgetExceededException>();
        advisor.Calls.Should().Be(2);
        diagnosis.UsedToday.Should().Be(2);

        clock.Now = clock.Now.AddDays(1);
        await diagnosis.AskAsync("pc", Checks, null, "");
        diagnosis.UsedToday.Should().Be(1);
    }
}
