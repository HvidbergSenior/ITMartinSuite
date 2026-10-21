using System.Net;
using FluentAssertions;
using ITMartinKunder.Client;
using ITMartinKunder.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ITMartinKunder.Tests;

public class KundekartotekTests
{
    private SqliteConnection _conn = null!;
    private Kundekartotek _k = null!;
    private FreeApps _apps = null!;

    private sealed class Factory(DbContextOptions<KunderDb> o) : IDbContextFactory<KunderDb>
    {
        public KunderDb CreateDbContext() => new(o);
    }

    [SetUp]
    public void SetUp()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<KunderDb>().UseSqlite(_conn).Options;
        using (var db = new KunderDb(options)) ITMartinKunder.Infrastructure.DependencyInjection.Upgrade(db);   // the real start-up path
        var f = new Factory(options);
        _k = new Kundekartotek(new CustomerRepository(f), TimeProvider.System);
        _apps = new FreeApps(new FreeAppRepository(f));
    }

    [TearDown] public void TearDown() => _conn.Dispose();

    private static SignUpRequest Yes(string email, string app, string company = Companies.ITMartin) =>
        new(email, "Ole", company, app, $"itmartin.dk: {app}", "Ja tak – skriv mig op", app);

    [Test]
    public async Task One_sign_up_per_person_one_code_and_interests_from_every_app()
    {
        (await _k.SignUpAsync(Yes("ikke-en-mail", "Tilbud"))).Should().NotBeNull();
        (await _k.SignUpAsync(Yes(" Ole@Gmail.com", "ElPriser"))).Should().BeNull();
        (await _k.SignUpAsync(Yes("ole@gmail.com", "Temperatur"))).Should().BeNull();

        var c = (await _k.ListAsync()).Should().ContainSingle().Subject;
        c.Email.Should().Be("ole@gmail.com");
        c.Code.Should().HaveLength(8);
        c.Interests.Should().Be("ElPriser ; Temperatur");
        c.Consents.Should().ContainSingle(x => x.Company == Companies.ITMartin);
        c.Apps.Should().Equal("ElPriser", "Temperatur");
    }

    [Test]
    public async Task A_buyer_is_a_customer_but_has_not_said_yes_and_the_same_order_counts_once()
    {
        var buy = new PurchaseRequest("kim@gmail.com", "Kim", "12345678", "Bogshoppen Salg", "tok-1", "Tintin", 120, DateTime.UtcNow);
        (await _k.BoughtAsync(buy)).Should().BeNull();
        (await _k.BoughtAsync(buy)).Should().BeNull();

        var c = (await _k.ListAsync()).Single();
        c.Events.Should().ContainSingle(e => e.Kind == EventKinds.Bought && e.Kr == 120);
        c.SaidYesTo(Companies.Bogshoppen).Should().BeFalse();
        (await _k.ActiveAsync(Companies.Bogshoppen)).Should().BeEmpty();
    }

    [Test]
    public async Task Afmeld_withdraws_every_yes_and_signing_up_again_lifts_it()
    {
        await _k.SignUpAsync(Yes("ole@gmail.com", "Stem"));
        await _k.SignUpAsync(Yes("ole@gmail.com", "Liveauktioner", Companies.Bogshoppen));
        (await _k.UnsubscribeAsync("OLE@gmail.com")).Should().BeTrue();
        (await _k.UnsubscribeAsync("ukendt@gmail.com")).Should().BeFalse();
        (await _k.ActiveAsync(Companies.ITMartin)).Should().BeEmpty();
        (await _k.ActiveAsync(Companies.Bogshoppen)).Should().BeEmpty();

        await _k.SignUpAsync(Yes("ole@gmail.com", "ElPriser"));
        (await _k.ActiveAsync(Companies.ITMartin)).Should().ContainSingle();
        (await _k.ActiveAsync(Companies.Bogshoppen)).Should().BeEmpty();   // only the yes they gave again
    }

    [Test]
    public async Task Import_keeps_the_old_afmeld_link_and_never_overwrites()
    {
        var token = new string('a', 32);
        var old = new ImportRequest("mie@gmail.com", "Mie", "", Companies.Bogshoppen, "Stem", "Stem: Første liveauktion", "Ja tak",
            new DateTime(2026, 10, 12), "Tintin", "", token, null, new DateTime(2026, 10, 12));
        (await _k.ImportAsync(old)).Should().BeNull();
        (await _k.ImportAsync(old with { Name = "Anden" })).Should().BeNull();

        var c = (await _k.ListAsync()).Single();
        c.Name.Should().Be("Mie");
        c.SaidYesTo(Companies.Bogshoppen).Should().BeTrue();
        (await _k.UnsubscribeByLinkAsync(token)).Should().Be("mie@gmail.com");
    }

    [Test]
    public async Task The_free_apps_start_as_the_three_the_user_named_and_can_be_switched_off()
    {
        (await _apps.AvailableAsync()).Select(a => a.Name).Should().Equal("ElPriser", "Temperatur", "Stem");
        var stem = (await _apps.AllAsync()).Single(a => a.Name == "Stem");
        stem.Available = false;
        await _apps.SaveAsync(stem);
        (await _apps.AvailableAsync()).Select(a => a.Name).Should().Equal("ElPriser", "Temperatur");
    }
}

public class KunderClientTests
{
    private sealed class Down : HttpMessageHandler
    {
        public bool IsDown = true;
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            if (IsDown) throw new HttpRequestException("Connection refused");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    [Test]
    public async Task When_Kunder_is_down_the_sign_up_waits_in_the_outbox_and_is_sent_later()
    {
        var dir = Path.Combine(Path.GetTempPath(), "kunder-outbox-" + Guid.NewGuid().ToString("N"));
        var handler = new Down();
        var outbox = new KunderOutbox(dir);
        var client = new KunderClient(new HttpClient(handler) { BaseAddress = new Uri("http://kunder") }, outbox, NullLogger<KunderClient>.Instance);
        try
        {
            (await client.SignUpAsync(new("ikke-mail", "", Companies.ITMartin, "Stem", "s", "c", ""))).Should().NotBeNull();
            (await client.SignUpAsync(new("ole@gmail.com", "Ole", Companies.ITMartin, "Stem", "s", "c", ""))).Should().BeNull();   // the person sees "tak"
            outbox.Count.Should().Be(1);

            await FluentActions.Invoking(() => outbox.FlushAsync(client, NullLogger.Instance, default)).Should().ThrowAsync<HttpRequestException>();
            outbox.Count.Should().Be(1);

            handler.IsDown = false;
            (await outbox.FlushAsync(client, NullLogger.Instance, default)).Should().Be(1);
            outbox.Count.Should().Be(0);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
