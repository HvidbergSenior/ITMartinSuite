using System.Net;
using System.Net.Http.Json;
using EasyPass.Application;
using EasyPass.Domain;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EasyPass.Tests;

public class MatchTests
{
    private static Sted Med(params (string, Svar)[] f) => new() { Navn = "X", By = "Aarhus", Faciliteter = f.ToDictionary(x => x.Item1, x => x.Item2) };

    [Test]
    public void Everything_there_passes()
    {
        var m = Matcher.Vurder(Med(("niveaufri", Svar.Ja), ("elevator", Svar.Ja), ("toilet", Svar.Ja)), Katalog.Laes("korestol"));
        m.Pasning.Should().Be(Pasning.Passer);
        m.Ord.Should().Be("Passer til dig");
    }

    [Test]
    public void One_missing_thing_means_it_does_not_fit_and_says_what()
    {
        var m = Matcher.Vurder(Med(("niveaufri", Svar.Ja), ("elevator", Svar.Ja), ("toilet", Svar.Nej)), Katalog.Laes("korestol"));
        m.Pasning.Should().Be(Pasning.PasserIkke);
        m.Mangler.Should().ContainSingle().Which.Should().Be("Handicaptoilet");
    }

    [Test]
    public void Not_checked_is_never_a_promise()
    {
        var m = Matcher.Vurder(Med(("niveaufri", Svar.Ja)), Katalog.Laes("korestol"));
        m.Pasning.Should().Be(Pasning.TjekSelv);
        m.Ukendt.Should().Contain("Handicaptoilet");
    }

    [Test]
    public void A_need_no_place_can_answer_is_check_yourself()
    {
        Matcher.Vurder(Med(), Katalog.Laes("arm-haand")).Pasning.Should().Be(Pasning.TjekSelv);
    }

    [Test]
    public void Unknown_need_keys_are_ignored() => Katalog.Laes("korestol,findes-ikke, larm").Select(b => b.Noegle).Should().Equal("korestol", "larm");

    [Test]
    public void Every_need_asks_only_for_known_facilities() =>
        Katalog.Alle.SelectMany(b => b.Kraever).Should().OnlyContain(k => Katalog.FindFacilitet(k) != null);
}

public class PlanTests
{
    [Test]
    public void Unchecked_things_become_questions_to_ask_and_the_card_goes_in_the_bag()
    {
        var sted = new Sted { Navn = "X", By = "Aarhus", Faciliteter = new() { ["niveaufri"] = Svar.Ja } };
        var plan = Planlaegger.Lav(sted, Katalog.Laes("korestol,ledsager"));
        plan.SpoergOm.Should().Contain("Har I et handicaptoilet?").And.Contain("Kommer en ledsager gratis ind med ledsagerkort?");
        plan.SpoergOm.Should().NotContain(q => q.Contains("trin"));   // checked = no need to ask
        plan.TagMed.Should().Equal("Dit ledsagerkort");
        plan.Raad.Should().StartWith("Ring eller skriv");
    }

    [Test]
    public void Everything_checked_means_no_calls_and_a_good_trip()
    {
        var sted = new Sted { Navn = "X", By = "Aarhus", Faciliteter = new() { ["toilet"] = Svar.Ja } };
        var plan = Planlaegger.Lav(sted, Katalog.Laes("toilet"));
        plan.SpoergOm.Should().BeEmpty();
        plan.Raad.Should().Be("Stedet har det, du har brug for. God tur!");
    }

    [Test]
    public void Every_facility_has_a_question() => Katalog.Faciliteter.Should().OnlyContain(f => f.Spoergsmaal.EndsWith("?"));
}

public class RulesTests
{
    [Test]
    public void A_place_needs_a_name_a_town_and_a_danish_position()
    {
        var p = StedRegler.Problemer(new Sted { Postnr = "80", Lat = 40, Lng = 10, Kategori = "Andet" });
        p.Should().Contain("Stedet skal have et navn.").And.Contain("Postnummer er fire cifre.").And.Contain("Placeringen ligger ikke i Danmark.");
    }

    [Test]
    public void Aarhus_to_Silkeborg_is_about_40_km() => StedRegler.Km(56.157, 10.204, 56.169, 9.545).Should().BeInRange(38, 43);

    [Test]
    public void An_owner_message_needs_a_mail() =>
        Tilbagemelding.Problem("Vi har fået elevator", null, TilbagemeldingType.Ejer).Should().NotBeNull();
}

public class ImportTests
{
    private sealed class MemStore : IStedStore
    {
        public Dictionary<string, Sted> Items { get; } = [];
        public IReadOnlyList<Sted> Alle() => Items.Values.ToList();
        public Sted? Hent(string id) => Items.GetValueOrDefault(id);
        public void Gem(Sted sted) => Items[sted.Id] = sted;
        public void GemMange(IEnumerable<Sted> steder) { foreach (var s in steder) Items[s.Id] = s; }
        public bool Slet(string id) => Items.Remove(id);
    }

    [Test]
    public void Old_easy_pass_columns_are_read_and_a_second_import_updates()
    {
        var store = new MemStore();
        var import = new ImportSteder(store, TimeProvider.System);
        var csv = "name;city;postal_code;latitude;longitude;wheelchair_accessible;has_accessible_restroom;stille-rum\n"
                + "\"Kunstmuseet; nord\";Aalborg;9000;57,04;9,92;true;false;ja\n"
                + "Uden by;;;;;;;\n";
        var r = import.Udfoer(csv);
        r.Nye.Should().Be(1);
        r.Sprunget.Should().ContainSingle().Which.Should().Contain("Uden by");
        var s = store.Alle().Single();
        s.Navn.Should().Be("Kunstmuseet; nord");
        s.Lat.Should().Be(57.04);
        s.Har("niveaufri").Should().Be(Svar.Ja);
        s.Har("toilet").Should().Be(Svar.Nej);
        s.Har("stille-rum").Should().Be(Svar.Ja);
        s.Har("elevator").Should().Be(Svar.Ukendt);

        import.Udfoer("navn,postnr,by,elevator\nKunstmuseet; nord,9000,Aalborg,ja\n").Opdateret.Should().Be(1);
        store.Alle().Should().ContainSingle().Which.Har("elevator").Should().Be(Svar.Ja);
    }
}

public class ApiTests
{
    private WebApplicationFactory<Program> _app = null!;
    private string _dir = "";

    [SetUp]
    public void Start()
    {
        _dir = Path.Combine(Path.GetTempPath(), "easypass-test-" + Guid.NewGuid().ToString("N"));
        _app = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("EasyPass:DataDir", _dir);
            b.UseSetting("EasyPass:AdminPin", "123456");
        });
    }

    [TearDown]
    public void Stop()
    {
        _app.Dispose();
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    [Test]
    public async Task Pages_and_search_answer()
    {
        var c = _app.CreateClient();
        foreach (var page in new[] { "/", "/soeg", "/pas", "/tur", "/admin", "/om" })
            (await c.GetAsync(page)).StatusCode.Should().Be(HttpStatusCode.OK, page);

        var r = await c.GetFromJsonAsync<SoegResultat>("/api/steder?behov=korestol");
        r!.Antal.Should().Be(5);   // the five examples
        r.Steder.Should().OnlyContain(s => s.Eksempel);
        r.Steder[0].Pasning.Should().Be("Passer");   // the museum has everything a wheelchair user needs
    }

    [Test]
    public async Task Admin_needs_the_pin()
    {
        var c = _app.CreateClient();
        (await c.PostAsync("/api/admin/tjek", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        c.DefaultRequestHeaders.Add("X-Pin", "123456");
        (await c.PostAsync("/api/admin/tjek", null)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task A_visitor_message_reaches_admin()
    {
        var c = _app.CreateClient();
        var id = (await c.GetFromJsonAsync<SoegResultat>("/api/steder?q=Havnecaf"))!.Steder[0].Id;
        (await c.PostAsJsonAsync($"/api/steder/{id}/besked", new { tekst = "Der er et trin", mail = "", ejer = false })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await c.PostAsJsonAsync($"/api/steder/{id}/besked", new { godt = true })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await c.GetFromJsonAsync<StedDetaljeDto>($"/api/steder/{id}"))!.AntalGodt.Should().Be(1);
        c.DefaultRequestHeaders.Add("X-Pin", "123456");
        (await c.GetStringAsync("/api/admin/beskeder")).Should().Contain("Der er et trin").And.Contain("Det passede");
    }
}

internal sealed record StedDetaljeDto(int AntalGodt);
