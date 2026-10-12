using FluentAssertions;
using ITMartinForloebet.Application;
using ITMartinForloebet.Domain;
using ITMartinForloebet.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ITMartinForloebet.Tests;

public class SagerTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Factory(DbContextOptions<ForloebetDbContext> o) : IDbContextFactory<ForloebetDbContext>
    {
        public ForloebetDbContext CreateDbContext() => new(o);
    }

    private sealed class FakeAi : IForloebAi
    {
        public int Calls;
        public Task<Omskrivning> OmskrivAsync(string raw, Forloeb? existing, CancellationToken ct = default)
        { Calls++; return Task.FromResult(new Omskrivning { Tekst = raw, Part = "Borger", Titel = "Afslag", Modpart = "Kommunen", Omraade = "Kommune" }); }
        public Task<List<TrinForslag>> TrinFraDokumentAsync(Forloeb f, DokumentIndhold indhold, CancellationToken ct = default)
        { Calls++; return Task.FromResult(new List<TrinForslag> { new() { Dato = "2026-10-01", Part = "Modpart", Tekst = indhold.Text ?? "" }, new() { Dato = "", Part = "Borger", Tekst = "Svar" } }); }
        public Task<Analyse> AnalyserAsync(Forloeb f, IReadOnlyList<Forloeb> delteAfsluttede, CancellationToken ct = default)
        { Calls++; return Task.FromResult(new Analyse { HvadHandledeDetOm = "Et afslag", Resume = $"{f.Trin.Count} trin" }); }
        public Task<List<string>> ForeslaaLaeringAsync(Forloeb f, Udfald udfald, string udfaldTekst, CancellationToken ct = default)
        { Calls++; return Task.FromResult(new List<string> { "a", "b", "c" }); }
        public Task<string> OverskriftAsync(IReadOnlyList<Forloeb> delte, CancellationToken ct = default)
        { Calls++; return Task.FromResult($"{delte.Count} forløb"); }
    }

    private sealed class MemoryFiles : IDocumentFiles
    {
        public Dictionary<string, byte[]> Saved = [];
        public async Task<string> SaveAsync(string slug, string fileName, Stream data, CancellationToken ct)
        {
            using var ms = new MemoryStream(); await data.CopyToAsync(ms, ct);
            var name = $"{slug}-{Saved.Count}{Path.GetExtension(fileName)}"; Saved[name] = ms.ToArray(); return name;
        }
        public string PathFor(string storedName) => "/docs/" + storedName;
        public Task<DokumentIndhold?> ReadForAiAsync(Dokument d, CancellationToken ct) =>
            Task.FromResult(DokumentArt.IsText(d.ContentType, d.FileName) ? new DokumentIndhold("brevet", null, null, null) : null);
    }

    private SqliteConnection _conn = null!;
    private Clock _clock = null!;
    private FakeAi _ai = null!;
    private MemoryFiles _files = null!;
    private EfForloebStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        var o = new DbContextOptionsBuilder<ForloebetDbContext>().UseSqlite(_conn).Options;
        using (var db = new ForloebetDbContext(o)) db.Database.EnsureCreated();
        _store = new EfForloebStore(new Factory(o));
        _clock = new Clock(); _ai = new FakeAi(); _files = new MemoryFiles();
    }

    [TearDown]
    public void TearDown() => _conn.Dispose();

    private Sager NewSager(int maxAi = 100, ForloebLimits? limits = null) =>
        new(_store, _files, _ai, new AiBudget(maxAi, _clock, NullLogger<AiBudget>.Instance), limits ?? new ForloebLimits(), _clock, NullLogger<Sager>.Instance);

    [Test]
    public void Rules_need_the_key_and_an_open_case()
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var f = Forloeb.Opret(new Omskrivning { Tekst = " Første ", Part = "Modpart", Modpart = "Movia", Omraade = "Kontrolafgift" }, now.AddDays(-10), false, now);
        f.Slug.Should().StartWith("kontrolafgift-movia-");
        f.Titel.Should().Be("Kontrolafgift");
        f.Trin.Single().Part.Should().Be(Part.Modpart);
        var limits = new ForloebLimits(MaxTrin: 2);

        f.TilfoejTrin("forkert", now, Part.Borger, "x", "", limits, now).Should().BeFalse();
        f.TilfoejTrin(f.EditKey, now.AddDays(-20), Part.Borger, "tidligere", "", limits, now).Should().BeTrue();
        f.StartedAt.Should().Be(now.AddDays(-20), "an earlier step moves the start");
        f.TilfoejTrin(f.EditKey, now, Part.Borger, "for mange", "", limits, now).Should().BeFalse();

        f.Afslut(f.EditKey, Udfald.Frafaldet, " Frafaldet ", " Bed om dokumentation ", true, now).Should().BeTrue();
        f.ClosedAt.Should().Be(now);
        f.TilfoejTrin(f.EditKey, now, Part.Borger, "efter lukning", "", new ForloebLimits(), now).Should().BeFalse();
        f.Afslut(f.EditKey, Udfald.Delvist, "", "", true, now.AddDays(3)).Should().BeTrue();
        f.ClosedAt.Should().Be(now, "closing again keeps the first close date");

        ForloebRules.UdfaldTekst(Udfald.Frafaldet).Should().Be("frafaldet");
        ForloebRules.ParsePart(" ANDEN ").Should().Be(Part.Anden);
        ForloebRules.AnalyseAf("ikke json").Tom.Should().BeTrue();
    }

    [Test]
    public async Task A_case_is_opened_extended_and_analysed_in_the_background()
    {
        var sager = NewSager();
        var o = await sager.OmskrivAsync("Jeg fik afslag", null);
        var f = await sager.OpretAsync(o, _clock.GetUtcNow().UtcDateTime, delt: true);
        await sager.RefreshAsync(f.Id);

        var saved = await sager.GetAsync(f.Slug);
        saved!.Analyse.HvadHandledeDetOm.Should().Be("Et afslag");
        saved.Resume.Should().Be("1 trin");
        (await sager.OverskriftAsync()).Should().Be("1 forløb");

        (await sager.TilfoejTrinAsync(f.Slug, "forkert", DateTime.UtcNow, Part.Borger, "x", "")).Should().BeFalse();
        (await sager.TilfoejTrinAsync(f.Slug, f.EditKey, DateTime.UtcNow, Part.Borger, "Klage sendt", "")).Should().BeTrue();
        var twoSteps = await sager.GetAsync(f.Slug);
        twoSteps!.Trin.Should().HaveCount(2);

        (await sager.SletTrinAsync(f.Slug, f.EditKey, twoSteps.Trin.First(t => t.Tekst == "Klage sendt").Id)).Should().BeTrue();
        var one = await sager.GetAsync(f.Slug);
        one!.Trin.Should().ContainSingle();
        (await sager.SletTrinAsync(f.Slug, f.EditKey, one.Trin[0].Id)).Should().BeFalse("the last step stays");
    }

    [Test]
    public async Task Documents_are_owner_only_and_their_suggestions_become_steps()
    {
        var sager = NewSager(limits: new ForloebLimits(MaxFileBytes: 100, MaxDokumenter: 1));
        var f = await sager.OpretAsync(new Omskrivning { Tekst = "Start" }, _clock.GetUtcNow().UtcDateTime, false);

        (await sager.GemDokumentAsync(f.Slug, f.EditKey, "stor.txt", "text/plain", 500, new MemoryStream(new byte[500]), default)).Should().BeNull("too big");
        (await sager.GemDokumentAsync(f.Slug, "forkert", "brev.txt", "text/plain", 5, new MemoryStream(new byte[5]), default)).Should().BeNull();
        var d = await sager.GemDokumentAsync(f.Slug, f.EditKey, "brev.txt", "text/plain", 5, new MemoryStream(new byte[5]), default);
        d.Should().NotBeNull();
        d!.Id.Should().BeGreaterThan(0);
        (await sager.GemDokumentAsync(f.Slug, f.EditKey, "to.txt", "text/plain", 5, new MemoryStream(new byte[5]), default)).Should().BeNull("max one");

        (await sager.DokumentFilAsync(f.Slug, d.Id, null)).Should().BeNull();
        (await sager.DokumentFilAsync(f.Slug, d.Id, f.EditKey))!.Value.Path.Should().Be("/docs/" + d.StoredName);

        var forslag = await sager.ForeslaaTrinAsync(f, d, null, default);
        forslag.Should().HaveCount(2);
        forslag[0].Tekst.Should().Be("brevet");
        await sager.TilfoejForslagAsync(f.Slug, f.EditKey, forslag);
        var after = await sager.GetAsync(f.Slug);
        after!.Trin.Should().HaveCount(3);
        after.Trin.Single(t => t.Tekst == "brevet").At.Should().Be(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        after.Trin.Single(t => t.Tekst == "Svar").At.Should().Be(_clock.GetUtcNow().UtcDateTime, "no date -> now");
    }

    [Test]
    public async Task AI_help_stops_at_the_days_budget()
    {
        var sager = NewSager(maxAi: 1);
        await sager.OmskrivAsync("a", null);
        await sager.Invoking(s => s.OmskrivAsync("b", null)).Should().ThrowAsync<AiBudgetExceededException>();
        _ai.Calls.Should().Be(1);
    }
}
