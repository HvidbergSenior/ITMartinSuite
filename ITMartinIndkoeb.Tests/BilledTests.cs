using FluentAssertions;
using ITMartinIndkoeb.Application;
using ITMartinIndkoeb.Domain;

namespace ITMartinIndkoeb.Tests;

// Upload billeder (2026-11-19): receipts -> purchase with "Tjek" notes, offer screenshots -> on the list under the shop.
public class BilledTests
{
    private sealed class Lager : IBilledLager
    {
        public Dictionary<string, byte[]> Filer { get; } = [];
        public async Task<string> GemAsync(string id, Stream data, string navn, CancellationToken ct = default)
        { var ms = new MemoryStream(); await data.CopyToAsync(ms, ct); var f = Guid.NewGuid().ToString("N") + ".jpg"; Filer[f] = ms.ToArray(); return f; }
        public Stream? Aabn(string id, string fil) => Filer.TryGetValue(fil, out var b) ? new MemoryStream(b) : null;
        public void Slet(string id, string fil) => Filer.Remove(fil);
    }

    private sealed class Laeser(params BilledLaesning[] svar) : IBilledLaeser
    {
        private int _i;
        public bool Klar => true;
        public Task<BilledLaesning> LaesAsync(Stream b, string f, Sted? s, CancellationToken ct = default) => Task.FromResult(svar[Math.Min(_i++, svar.Length - 1)]);
    }

    private static BilledLaesning Kvittering(decimal total, params string[] tjek) =>
        new("kvittering", "lidl", total, DateOnly.FromDateTime(DateTime.Today), tjek, null, null, null, false, null, null);

    private (KvitteringTests.Mem store, Husstand h, Billeder uc, Lager lager) Ny(params BilledLaesning[] svar)
    {
        var store = new KvitteringTests.Mem(); var h = Husstand.Ny("X", DateTime.UtcNow); store.Gem(h);
        var lager = new Lager();
        return (store, h, new Billeder(store, lager, new Laeser(svar), new Indkoeb(store, TimeProvider.System), TimeProvider.System), lager);
    }

    [Test]
    public async Task A_receipt_picture_becomes_a_purchase_with_tjek_notes()
    {
        var (store, h, uc, _) = Ny(Kvittering(85.45m, "BKI KAFFE: tilbud 30 kr hos Lidl – du betalte 39,95 kr."));
        (await uc.UploadAsync(h.Noegle, new MemoryStream([1, 2, 3]), "IMG_1.jpg", 3, "Vibz")).Should().BeNull();
        (await uc.LaesVentendeAsync()).Should().Be(1);
        var x = store.Hent(h.Noegle)!;
        x.Koeb.Should().ContainSingle().Which.Should().Match<Koeb>(k => k.Beloeb == 85.45m && k.Butik == "Lidl" && k.Hvem == "Vibz");
        var b = x.Billeder.Single();
        b.Status.Should().Be(BilledStatus.Laest);
        b.Tjek.Should().ContainSingle();
    }

    [Test]
    public async Task An_offer_screenshot_goes_on_the_list_under_its_shop()
    {
        var (store, h, uc, _) = Ny(new BilledLaesning("tilbud", null, null, null, [], "Lidl", "BKI Kaffe formalet", 24.95m, true, "Lidl Plus", null));
        await uc.UploadAsync(h.Noegle, new MemoryStream([1]), "skaermbillede.png", 1, "Martin");
        await uc.LaesVentendeAsync();
        var v = store.Hent(h.Noegle)!.Seddel.Single();
        v.Tekst.Should().Be("BKI Kaffe formalet"); v.Butik.Should().Be("Lidl"); v.Tilbud.Should().Contain("kun med Lidl Plus");
    }

    [Test]
    public async Task Kvittering_down_is_tried_again_then_given_up()
    {
        var down = new BilledLaesning("ukendt", null, null, null, [], null, null, null, false, null, "Kvittering svarer ikke lige nu.");
        var (store, h, uc, _) = Ny(down);
        await uc.UploadAsync(h.Noegle, new MemoryStream([1]), "a.jpg", 1, "");
        for (var i = 0; i < Billede.MaxForsoeg; i++) await uc.LaesVentendeAsync();
        store.Hent(h.Noegle)!.Billeder.Single().Status.Should().Be(BilledStatus.Fejl);
    }

    [Test]
    public async Task Too_big_or_not_a_picture_is_refused_and_removing_deletes_the_file()
    {
        var (store, h, uc, lager) = Ny(Kvittering(10));
        (await uc.UploadAsync(h.Noegle, new MemoryStream([1]), "a.jpg", Billeder.MaxBytes + 1, "")).Should().NotBeNull();
        (await uc.UploadAsync(h.Noegle, new MemoryStream([1]), "virus.exe", 1, "")).Should().NotBeNull();
        await uc.UploadAsync(h.Noegle, new MemoryStream([1]), "a.jpg", 1, "");
        var id = store.Hent(h.Noegle)!.Billeder.Single().Id;
        uc.Slet(h.Noegle, id).Should().BeTrue();
        lager.Filer.Should().BeEmpty();
    }
}
