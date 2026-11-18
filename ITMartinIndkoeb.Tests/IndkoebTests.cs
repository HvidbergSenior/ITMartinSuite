using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ITMartinIndkoeb.Application;
using ITMartinIndkoeb.Domain;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ITMartinIndkoeb.Tests;

public class UgeTests
{
    private static readonly DateOnly Torsdag = new(2026, 11, 19);
    private static Koeb K(int dag, decimal kr, string butik = "Lidl") => new(Guid.NewGuid().ToString("N")[..6], new DateOnly(2026, 11, dag), kr, butik, "", DateTime.UtcNow);

    [Test]
    public void The_week_starts_on_monday_and_counts_only_this_week()
    {
        var u = Regler.Uge([K(15, 999), K(16, 450), K(18, 80, "Føtex")], Maal.Standard, Torsdag);   // 15.11 is last Sunday
        u.Mandag.Should().Be(new DateOnly(2026, 11, 16));
        u.Brugt.Should().Be(530);
        u.Tilbage.Should().Be(220);
        u.Ture.Should().Be(2);
        u.StoreIndkoeb.Should().Be(1);
        u.Suppleringer.Should().Be(1);
    }

    [Test]
    public void No_big_shop_yet_says_make_it_first()
    {
        Regler.Uge([K(17, 60)], Maal.Standard, Torsdag).NaesteSkridt.Should().StartWith("Lav ugens store indkøb");
    }

    [Test]
    public void A_top_up_over_the_limit_is_warned_and_over_the_goal_is_over()
    {
        var u = Regler.Uge([K(16, 600), K(17, 180, "Føtex")], Maal.Standard, Torsdag);
        u.Advarsler.Should().ContainSingle().Which.Should().Contain("Føtex");
        u.Tilstand.Should().Be(UgeTilstand.Over);
        u.NaesteSkridt.Should().StartWith("Ugen er brugt");
    }

    [Test]
    public void Seven_fifty_a_week_is_three_thousand_to_thirty_five_hundred_a_month()
    {
        Maal.Standard.MaanedFra.Should().Be(3000);
        Maal.Standard.MaanedTil.Should().Be(3500);
    }

    [Test]
    public void A_purchase_needs_an_amount_a_shop_and_no_future_date()
    {
        Regler.KoebProblem(0, "Lidl", Torsdag, Torsdag).Should().NotBeNull();
        Regler.KoebProblem(100, "", Torsdag, Torsdag).Should().NotBeNull();
        Regler.KoebProblem(100, "Lidl", Torsdag.AddDays(1), Torsdag).Should().NotBeNull();
        Regler.KoebProblem(100, "Lidl", Torsdag, Torsdag).Should().BeNull();
    }
}

public class ApiTests
{
    private WebApplicationFactory<Program> _app = null!;
    private string _dir = "";

    [SetUp]
    public void Start()
    {
        _dir = Path.Combine(Path.GetTempPath(), "indkoeb-test-" + Guid.NewGuid().ToString("N"));
        _app = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("Indkoeb:DataDir", _dir));
    }

    [TearDown]
    public void Stop()
    {
        _app.Dispose();
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    private sealed record Ny(string Noegle, string Link);

    [Test]
    public async Task A_family_makes_a_page_adds_a_purchase_and_everyone_sees_it()
    {
        var c = _app.CreateClient();
        foreach (var p in new[] { "/", "/kolibri/om" }) (await c.GetAsync(p)).StatusCode.Should().Be(HttpStatusCode.OK, p);
        var ny = await (await c.PostAsJsonAsync("/api/husstand", new { navn = "Familien Test" })).Content.ReadFromJsonAsync<Ny>();
        (await c.GetAsync(ny!.Link)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await c.PostAsJsonAsync($"/api/h/{ny.Noegle}/koeb", new { beloeb = 245.5m, butik = "Lidl", hvem = "Martin" })).StatusCode.Should().Be(HttpStatusCode.OK);
        var other = _app.CreateClient();   // another phone in the family
        var json = await other.GetStringAsync($"/api/h/{ny.Noegle}");
        json.Should().Contain("Familien Test").And.Contain("245.5").And.Contain("Lidl");

        (await c.GetAsync("/api/h/AAAAAAAAAAAAAAAAAAAAAAAA")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task A_bad_goal_is_refused_with_a_reason()
    {
        var c = _app.CreateClient();
        var ny = await (await c.PostAsJsonAsync("/api/husstand", new { navn = "X" })).Content.ReadFromJsonAsync<Ny>();
        var r = await c.PutAsJsonAsync($"/api/h/{ny!.Noegle}/maal", new { ugeMaal = 750, maxTure = 5, supplGraense = 300, stortFra = 250 });
        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await r.Content.ReadAsStringAsync()).Should().Contain("stort indkøb");
    }
}
