using Microsoft.Extensions.DependencyInjection;
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

public class OpenStreetMapTests
{
    private sealed class Mem : IStedStore
    {
        public Dictionary<string, Sted> Items { get; } = [];
        public IReadOnlyList<Sted> Alle() => Items.Values.ToList();
        public Sted? Hent(string id) => Items.GetValueOrDefault(id);
        public void Gem(Sted s) => Items[s.Id] = s;
        public void GemMange(IEnumerable<Sted> l) { foreach (var s in l) Items[s.Id] = s; }
        public bool Slet(string id) => Items.Remove(id);
    }

    private sealed class FakeOsm(params Sted[] steder) : IOpenStreetMapKilde
    {
        public Task<IReadOnlyList<Sted>> HentAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Sted>>(steder);
    }

    private sealed class FakeBy(Punkt? p) : IByOpslag
    {
        public Task<Punkt?> FindAsync(string tekst, CancellationToken ct = default) => Task.FromResult(p);
    }

    private static Sted Osm(string id, string navn, double lat = 56.25, double lng = 9.86) =>
        new() { OsmId = id, Navn = navn, Lat = lat, Lng = lng, Kilde = "OpenStreetMap", Faciliteter = new() { ["niveaufri"] = Svar.Ja } };

    [Test]
    public void Overpass_tags_become_facts()
    {
        using var doc = System.Text.Json.JsonDocument.Parse("""
            {"type":"way","id":7,"center":{"lat":56.1,"lon":10.2},"tags":{"name":"Byens Bibliotek","amenity":"library","wheelchair":"yes","toilets:wheelchair":"no","hearing_loop":"yes"}}
            """);
        var s = EasyPass.Infrastructure.OverpassKilde.Map(doc.RootElement)!;
        s.OsmId.Should().Be("way/7");
        s.Kategori.Should().Be("Kultur og teater");
        s.Har("niveaufri").Should().Be(Svar.Ja);
        s.Har("toilet").Should().Be(Svar.Nej);
        s.Har("teleslynge").Should().Be(Svar.Ja);
        StedRegler.Problemer(s).Should().BeEmpty();   // position is enough, no address needed
    }

    [Test]
    public async Task Refresh_adds_updates_never_overwrites_our_edits_and_removes_examples()
    {
        var store = new Mem();
        store.Gem(new Sted { Navn = "Eksempel: X", By = "Aarhus", Kilde = "Eksempel" });
        var rettet = Osm("node/2", "Rettet af os"); rettet.Kilde = "Stedet selv"; store.Gem(rettet);
        var r = await new OpdaterFraOpenStreetMap(store, new FakeOsm(Osm("node/1", "Nyt"), Osm("node/2", "Fra OSM")), TimeProvider.System).UdfoerAsync();
        r.Nye.Should().Be(1);
        r.EksemplerSlettet.Should().Be(1);
        store.Alle().Single(s => s.OsmId == "node/2").Navn.Should().Be("Rettet af os");

        var tom = await new OpdaterFraOpenStreetMap(store, new FakeOsm(), TimeProvider.System).UdfoerAsync();
        tom.Fjernet.Should().Be(0);   // an empty answer never wipes the places
        store.Alle().Should().HaveCount(2);
    }

    [Test]
    public async Task A_town_with_no_name_match_shows_the_nearest_places()
    {
        var store = new Mem();
        store.Gem(Osm("node/1", "Café ved søen", 56.26, 9.87));    // ~1.5 km from Hammel
        store.Gem(Osm("node/2", "Langt væk", 55.0, 12.5));
        var find = new FindSteder(store, new FakeBy(new Punkt("Hammel", 56.257, 9.862)));
        var r = await find.UdfoerAsync("Hammel", null, "korestol", false, null, null);
        r.Ved.Should().Be("Hammel");
        r.Steder.Should().ContainSingle().Which.Navn.Should().Be("Café ved søen");
    }

    [Test]
    public async Task Not_a_town_falls_back_to_the_name()
    {
        var store = new Mem();
        store.Gem(Osm("node/1", "Kystmuseet"));
        var r = await new FindSteder(store, new FakeBy(null)).UdfoerAsync("Kystmus", null, null, false, null, null);
        r.Steder.Should().ContainSingle();
        r.Ved.Should().BeNull();
    }
}

public class SammenTests
{
    private sealed class FakeSnak : ISnakRum
    {
        public bool Klar => true;
        public List<(string Titel, IReadOnlyList<string> Navne, IReadOnlyList<TurTrin> Trin, string Slut)> Lavet { get; } = [];
        public Task<string?> OpretAsync(string titel, IReadOnlyList<string> navne, IReadOnlyList<TurTrin> trin, string slutDag, CancellationToken ct = default)
        {
            Lavet.Add((titel, navne, trin, slutDag));
            return Task.FromResult<string?>("https://snak.test/r/abc");
        }
    }

    private sealed class Mem : IStedStore
    {
        public Dictionary<string, Sted> Items { get; } = [];
        public IReadOnlyList<Sted> Alle() => Items.Values.ToList();
        public Sted? Hent(string id) => Items.GetValueOrDefault(id);
        public void Gem(Sted s) => Items[s.Id] = s;
        public void GemMange(IEnumerable<Sted> l) { foreach (var s in l) Items[s.Id] = s; }
        public bool Slet(string id) => Items.Remove(id);
    }

    private sealed class Clock(DateTime now) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(now); }

    [Test]
    public void The_trip_becomes_steps_on_the_right_days()
    {
        var sted = new Sted { Navn = "Kystmuseet", By = "Skagen", Telefon = "98 44 22 33", Faciliteter = new() { ["niveaufri"] = Svar.Ja } };
        var trin = TurTrinRegler.Lav(sted, Katalog.Laes("korestol,ledsager"), new DateOnly(2026, 12, 12), new DateOnly(2026, 12, 1));
        trin.Select(t => t.Dag).Should().Equal("2026-12-10", "2026-12-11", "2026-12-12", "2026-12-12");
        trin[0].Note.Should().Contain("Har I et handicaptoilet?").And.Contain("98 44 22 33");
        trin[1].Note.Should().Contain("Dit ledsagerkort");
    }

    [Test]
    public void A_trip_tomorrow_puts_the_call_today_not_in_the_past()
    {
        var sted = new Sted { Navn = "X", By = "Aarhus" };
        TurTrinRegler.Lav(sted, Katalog.Laes("toilet"), new DateOnly(2026, 12, 2), new DateOnly(2026, 12, 1))[0].Dag.Should().Be("2026-12-01");
    }

    [Test]
    public async Task Plan_together_makes_one_room_with_both_names_that_melts_after_the_trip()
    {
        var store = new Mem();
        var sted = new Sted { Navn = "Kystmuseet", By = "Skagen" };
        store.Gem(sted);
        var snak = new FakeSnak();
        var p = new PlanlaegSammen(store, snak, new Clock(new DateTime(2026, 12, 1, 9, 0, 0, DateTimeKind.Utc)));
        (await p.UdfoerAsync(sted.Id, "korestol", "2026-11-30", "Ali", "Lise")).Fejl.Should().Contain("Vælg den dag");
        (await p.UdfoerAsync(sted.Id, "korestol", "2026-12-12", "  ", "Lise")).Fejl.Should().Contain("fornavn");
        var ok = await p.UdfoerAsync(sted.Id, "korestol", "2026-12-12", "Ali", "Lise (pædagog)");
        ok.Link.Should().Be("https://snak.test/r/abc");
        snak.Lavet.Should().ContainSingle();
        snak.Lavet[0].Navne.Should().Equal("Ali", "Lise (pædagog)");
        snak.Lavet[0].Slut.Should().Be("2026-12-15");
        snak.Lavet[0].Titel.Should().Be("Tur: Kystmuseet 12.12.");
    }
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
            b.UseSetting("EasyPass:OsmAuto", "false");
            // No real Nominatim calls from tests.
            b.ConfigureServices(sv => sv.AddSingleton<IByOpslag>(new IngenBy()));
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

internal sealed class IngenBy : IByOpslag
{
    public Task<Punkt?> FindAsync(string tekst, CancellationToken ct = default) => Task.FromResult<Punkt?>(null);
}
