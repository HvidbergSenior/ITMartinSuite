using FluentAssertions;
using ITMartinIndkoeb.Application;
using ITMartinIndkoeb.Domain;

namespace ITMartinIndkoeb.Tests;

// The family's process (2026-11-19): one finds offers, another takes the trip, anyone snaps the receipt.
public class ProcesTests
{
    private KvitteringTests.Mem _store = null!;
    private Husstand _h = null!;
    private Indkoeb _uc = null!;

    [SetUp]
    public void Setup()
    {
        _store = new KvitteringTests.Mem();
        _h = Husstand.Ny("Skejby", DateTime.UtcNow);
        _store.Gem(_h);
        _uc = new Indkoeb(_store, TimeProvider.System);
    }

    private sealed class FakeTilbud : ITilbudKilde
    {
        public int Kald;
        public Task<IReadOnlyList<TilbudVisning>> SoegAsync(string q, double lat, double lng, CancellationToken ct = default)
        { Kald++; return Task.FromResult<IReadOnlyList<TilbudVisning>>([new("Netto", "BKI kaffe", null, 30, null, "75 kr/kg", null, null, null, null, null)]); }
        public Task<Sted?> StedAsync(string postnr, CancellationToken ct = default) =>
            Task.FromResult<Sted?>(postnr == "8200" ? new Sted("8200", "8200 Aarhus", 56.19, 10.19) : null);
    }

    private sealed class Laeser(KvitteringForslag svar) : IKvitteringLaeser
    {
        public bool Klar => true;
        public Task<KvitteringForslag> LaesAsync(Stream b, string f, CancellationToken ct = default) => Task.FromResult(svar);
    }

    [Test]
    public void The_list_is_grouped_by_shop_with_anywhere_last_and_no_double_lines()
    {
        _uc.TilfoejVare(_h.Noegle, "mælk", "Vibz", null, null).Should().BeNull();
        _uc.TilfoejVare(_h.Noegle, "BKI kaffe", "Julius", "REMA 1000", "30 kr").Should().BeNull();
        _uc.TilfoejVare(_h.Noegle, "Mælk", "Eigil", null, null).Should().BeNull();   // same line again -> not twice
        var g = Indkoeb.Grupper(_store.Hent(_h.Noegle)!);
        g.Select(x => x.Butik).Should().Equal("Rema 1000", SeddelRegler.HvorSomHelst);
        g[1].Varer.Should().ContainSingle();
        _uc.TilfoejVare(_h.Noegle, "  ", "x", null, null).Should().NotBeNull();
    }

    [Test]
    public void Someone_takes_the_trip_and_saving_the_purchase_clears_what_was_ticked_there()
    {
        _uc.TilfoejVare(_h.Noegle, "kaffe", "Vibz", "Netto", "30 kr");
        _uc.TilfoejVare(_h.Noegle, "løg", "Vibz", "Netto", null);
        _uc.TilfoejVare(_h.Noegle, "smør", "Vibz", "Lidl", null);
        _uc.TagTur(_h.Noegle, "Netto", "Eigil");
        var h = _store.Hent(_h.Noegle)!;
        Indkoeb.Grupper(h).Single(g => g.Butik == "Netto").Handler.Should().Be("Eigil");
        _uc.Afkryds(_h.Noegle, h.Seddel.Single(v => v.Tekst == "kaffe").Id, true);

        _uc.LaegInd(_h.Noegle, 180, "Netto", "Eigil", null).Should().BeNull();

        h = _store.Hent(_h.Noegle)!;
        h.Seddel.Select(v => v.Tekst).Should().BeEquivalentTo(["løg", "smør"]);   // unticked + other shop stay
        h.Handler.Should().NotContainKey("Netto");
    }

    [Test]
    public async Task Offers_need_the_familys_postnummer_first()
    {
        var t = new FakeTilbud();
        (await _uc.SoegTilbudAsync(_h.Noegle, "kaffe", t)).Fejl.Should().Contain("postnummer");
        (await _uc.SaetStedAsync(_h.Noegle, "123", t)).Should().NotBeNull();
        (await _uc.SaetStedAsync(_h.Noegle, "8200", t)).Should().BeNull();
        var svar = await _uc.SoegTilbudAsync(_h.Noegle, "kaffe", t);
        svar.Fejl.Should().BeNull();
        svar.Tilbud.Should().ContainSingle().Which.Butik.Should().Be("Netto");
        (await _uc.SoegTilbudAsync(_h.Noegle, "k", t)).Fejl.Should().NotBeNull();
        t.Kald.Should().Be(1);
    }

    [Test]
    public async Task A_picture_is_enough_the_purchase_is_saved_at_once()
    {
        var idag = DateOnly.FromDateTime(DateTime.UtcNow);
        var kv = new LaesKvittering(_store, new Laeser(new KvitteringForslag(245.5m, null, idag, "LIDL DANMARK", null)), _uc, TimeProvider.System);
        var f = await kv.LaesOgGemAsync(_h.Noegle, new MemoryStream([1]), "k.jpg", "Bertil");
        f.Gemt.Should().BeTrue();
        _store.Hent(_h.Noegle)!.Koeb.Should().ContainSingle().Which.Should().Match<Koeb>(k => k.Beloeb == 245.5m && k.Butik == "Lidl" && k.Hvem == "Bertil");
    }

    [Test]
    public async Task No_total_on_the_receipt_saves_nothing()
    {
        var kv = new LaesKvittering(_store, new Laeser(new KvitteringForslag(null, null, null, "Netto", "Totalen kunne ikke læses.")), _uc, TimeProvider.System);
        var f = await kv.LaesOgGemAsync(_h.Noegle, new MemoryStream([1]), "k.jpg", "Bertil");
        f.Gemt.Should().BeFalse();
        _store.Hent(_h.Noegle)!.Koeb.Should().BeEmpty();
    }
}

public class SkjulTests
{
    private sealed class Tilbud2 : ITilbudKilde
    {
        public Task<IReadOnlyList<TilbudVisning>> SoegAsync(string q, double lat, double lng, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TilbudVisning>>([
                new("Bilka", "Arla mælk", null, 9, null, null, null, null, null, null, null),
                new("Netto", "Arla mælk", null, 10, null, null, null, null, null, null, null)]);
        public Task<Sted?> StedAsync(string p, CancellationToken ct = default) => Task.FromResult<Sted?>(new Sted(p, p, 56, 10));
    }

    [Test]
    public async Task A_hidden_shop_is_not_shown_and_can_come_back()
    {
        var store = new KvitteringTests.Mem(); var h = Husstand.Ny("X", DateTime.UtcNow); store.Gem(h);
        var uc = new Indkoeb(store, TimeProvider.System); var t = new Tilbud2();
        await uc.SaetStedAsync(h.Noegle, "8200", t);
        uc.SkjulButik(h.Noegle, "Bilka", true).Should().BeNull();
        (await uc.SoegTilbudAsync(h.Noegle, "mælk", t)).Tilbud.Select(o => o.Butik).Should().Equal("Netto");
        uc.SkjulButik(h.Noegle, "bilka", false);
        (await uc.SoegTilbudAsync(h.Noegle, "mælk", t)).Tilbud.Should().HaveCount(2);
    }
}
