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
        c.Code.Should().HaveLength(8).And.MatchRegex("^[1-9][0-9]{7}$");
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
    public async Task The_kundenummer_opens_the_free_apps_until_they_unsubscribe()
    {
        var (_, code) = await _k.SignUpWithCodeAsync(Yes("ole@gmail.com", "ElPriser"));
        var apps = await _apps.AllAsync();
        (await _k.CheckCodeAsync(KundeWords.ShowCode(code), "elpriser", apps)).Ok.Should().BeTrue();   // "4821 7730" typed with a space
        (await _k.CheckCodeAsync(code, "Tilbud", apps)).Error.Should().Contain("ikke med");
        (await _k.CheckCodeAsync("12345678", "Stem", apps)).Error.Should().Contain("kender vi ikke");
        (await _k.SignUpWithCodeAsync(Yes("ole@gmail.com", "Stem"))).Code.Should().Be(code);   // same number when they sign up again

        await _k.UnsubscribeAsync("ole@gmail.com");
        (await _k.CheckCodeAsync(code, "Stem", apps)).Error.Should().Contain("afmeldt");

        // A Bogshoppen yes opens the free apps too; a buyer who never ticked the box does not.
        var (_, bs) = await _k.SignUpWithCodeAsync(Yes("mie@gmail.com", "Liveauktioner", Companies.Bogshoppen));
        (await _k.CheckCodeAsync(bs, "ElPriser", apps)).Ok.Should().BeTrue();
        await _k.BoughtAsync(new PurchaseRequest("kim@gmail.com", "Kim", "", "Bogshoppen Salg", "t1", "Tintin", 80, DateTime.UtcNow));
        (await _k.ListAsync()).Single(c => c.Email == "kim@gmail.com").CodeWorks.Should().BeFalse();
    }

    [Test]
    public async Task Every_working_kundenummer_opens_the_bogshoppen_market()
    {
        var apps = await _apps.AllAsync();
        var (_, it) = await _k.SignUpWithCodeAsync(Yes("ole@gmail.com", "ElPriser"));
        var (_, bs) = await _k.SignUpWithCodeAsync(Yes("mie@gmail.com", "Liveauktioner", Companies.Bogshoppen));
        (await _k.CheckCodeAsync(it, KunderRoles.Market, apps)).Ok.Should().BeTrue();
        (await _k.CheckCodeAsync(bs, "bogshoppen", apps)).Ok.Should().BeTrue();
        (await _k.CheckCodeAsync("12345678", KunderRoles.Market, apps)).Error.Should().Contain("kender vi ikke");

        await _k.UnsubscribeAsync("mie@gmail.com");
        (await _k.CheckCodeAsync(bs, KunderRoles.Market, apps)).Error.Should().Contain("afmeldt");
    }

    [Test]
    public async Task The_free_apps_start_as_the_three_the_user_named_and_can_be_switched_off()
    {
        (await _apps.AvailableAsync()).Select(a => (a.Name, a.NeedsCode)).Should().Equal(("ElPriser", true), ("Temperatur", false), ("Stem", true));
        var stem = (await _apps.AllAsync()).Single(a => a.Name == "Stem");
        stem.Available = false;
        await _apps.SaveAsync(stem);
        (await _apps.AvailableAsync()).Select(a => a.Name).Should().Equal("ElPriser", "Temperatur");
    }

    [Test]
    public void Retention_keeps_a_yes_deletes_afmeldt_after_30_days_and_keeps_purchases_5_years()
    {
        var now = new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);
        Customer Yes() { var c = Customer.New("a@b.dk", "", now.AddYears(-3)); c.Consents.Add(new Consent { Company = Companies.ITMartin, At = now.AddYears(-3) }); return c; }
        Retention.ShouldDelete(Yes(), now).Should().BeFalse();

        var left = Yes(); left.Unsubscribe(now.AddDays(-29));
        Retention.ShouldDelete(left, now).Should().BeFalse();
        left.UnsubscribedAt = now.AddDays(-31);
        Retention.ShouldDelete(left, now).Should().BeTrue();

        left.Events.Add(CustomerEvent.Of(EventKinds.Bought, "Salg", "o1", "Bog", 50, now.AddYears(-4)));
        Retention.ShouldDelete(left, now).Should().BeFalse();                   // bought 4 years ago: kept for bogføringsloven
        Retention.ShouldDelete(left, now.AddYears(1).AddDays(1)).Should().BeTrue();

        var buyer = Customer.New("k@b.dk", "", now.AddYears(-6));
        buyer.Events.Add(CustomerEvent.Of(EventKinds.Bought, "Salg", "o2", "Bog", 50, now.AddYears(-2)));
        Retention.ShouldDelete(buyer, now).Should().BeFalse();
        Retention.ShouldDelete(buyer, now.AddYears(3).AddDays(1)).Should().BeTrue();
    }

    [Test]
    public async Task Hent_mine_data_has_everything_and_clean_up_leaves_people_with_a_yes()
    {
        await _k.SignUpAsync(Yes("ole@gmail.com", "ElPriser"));
        var ole = (await _k.ListAsync()).Single();
        var x = await _k.ExportAsync(ole.Id);
        x!.Email.Should().Be("ole@gmail.com");
        x.Samtykker.Should().ContainSingle(s => s.Company == Companies.ITMartin);
        x.Haendelser.Should().ContainSingle(e => e.App == "ElPriser");
        (await _k.CleanUpAsync()).Should().Be(0);
        await _k.UnsubscribeAsync("ole@gmail.com");
        (await _k.CleanUpAsync()).Should().Be(0);                               // afmeldt today: still kept 30 days
    }

    [Test]
    public async Task A_kundenummer_opens_Vareinfo_and_Lager_only_while_Martin_has_ticked_Hjaelper()
    {
        var (_, code) = await _k.SignUpWithCodeAsync(Yes("ole@gmail.com", "ElPriser"));
        var apps = await _apps.AllAsync();
        (await _k.CheckCodeAsync(code, KunderRoles.Helper, apps)).Ok.Should().BeFalse();
        var ole = (await _k.ListAsync()).Single();
        await _k.SetHelperAsync(ole.Id, true);
        var ok = await _k.CheckCodeAsync(KundeWords.ShowCode(code), "hjælper", apps);
        (ok.Ok, ok.Name).Should().Be((true, "Ole"));
        await _k.UnsubscribeAsync("ole@gmail.com");
        (await _k.CheckCodeAsync(code, KunderRoles.Helper, apps)).Ok.Should().BeFalse();   // afmeldt = no more access
    }

    private sealed class FakeMail : IMailSender
    {
        public bool Works = true;
        public List<(string To, string Subject, string Body)> Sent = [];
        public Task<bool> SendAsync(string to, string subject, string body, CancellationToken ct = default)
        {
            if (Works) Sent.Add((to, subject, body));
            return Task.FromResult(Works);
        }
    }

    [Test]
    public async Task The_received_letter_goes_once_to_each_customer_with_their_own_number_and_afmeld_link()
    {
        await _k.SignUpAsync(Yes("ole@gmail.com", "ElPriser"));
        await _k.SignUpAsync(Yes("bo@gmail.com", "bogshoppen.dk", Companies.Bogshoppen));
        await _k.SignUpAsync(Yes("gone@gmail.com", "Stem"));
        await _k.UnsubscribeAsync("gone@gmail.com");
        await _k.BoughtAsync(new PurchaseRequest("buyer@gmail.com", "Kim", "", "Salg", "o1", "Bog", 50, DateTime.UtcNow));

        var f = new Factory(new DbContextOptionsBuilder<KunderDb>().UseSqlite(_conn).Options);
        var mail = new FakeMail { Works = false };
        var breve = new KundeBreve(new CustomerRepository(f), mail, TimeProvider.System);
        var waiting = await breve.WaitingAsync(Letters.Received);
        waiting.Select(c => c.Email).Should().BeEquivalentTo(["ole@gmail.com", "bo@gmail.com"]);   // not the unsubscribed, not a buyer without a yes

        (await breve.SendReceivedAsync(waiting[0].Id, "https://kunder.itmartin.dk")).Should().BeFalse();   // mail server down: still waiting
        (await breve.WaitingAsync(Letters.Received)).Should().HaveCount(2);

        mail.Works = true;
        foreach (var c in waiting) (await breve.SendReceivedAsync(c.Id, "https://kunder.itmartin.dk/")).Should().BeTrue();
        foreach (var c in waiting) (await breve.SendReceivedAsync(c.Id, "https://kunder.itmartin.dk/")).Should().BeFalse();   // never twice
        mail.Sent.Should().HaveCount(2);
        mail.Sent[0].Body.Should().Contain(KundeWords.ShowCode(waiting[0].Code)).And.Contain($"https://kunder.itmartin.dk/afmeld/{waiting[0].UnsubscribeToken}");
        (await breve.WaitingAsync(Letters.Received)).Should().BeEmpty();
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
