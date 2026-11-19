using ITMartinIndkoeb.Domain;

namespace ITMartinIndkoeb.Application;

/// <summary>"Upload billeder" (user 2026-11-19: "upload pictures will be the main data input"): photos taken with the phone's
/// own camera - receipts and screenshots of offers (Lidl app ...) - are uploaded when it suits, read in the background by
/// Kvittering (free OCR + rules), and become a purchase (receipt, with "Tjek" notes on missed discounts) or a line on the
/// list under the shop (offer). Anything else waits with "Hvad er det?".</summary>
public sealed class Billeder(IHusstandStore store, IBilledLager lager, IBilledLaeser laeser, Indkoeb indkoeb, TimeProvider clock)
{
    public const long MaxBytes = 12 * 1024 * 1024;
    public const int MaxPrGang = 20;

    public async Task<string?> UploadAsync(string noegle, Stream data, string filnavn, long laengde, string? hvem, CancellationToken ct = default)
    {
        if (store.Hent(noegle) is not { } h) return "Siden findes ikke.";
        if (laengde <= 0) return "Billedet er tomt.";
        if (laengde > MaxBytes) return "Billedet er for stort (højst 12 MB).";
        var ext = Path.GetExtension(filnavn).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".heic" or "")) return "Kun billeder (jpg, png, heic).";
        var fil = await lager.GemAsync(h.Id, data, filnavn, ct);
        h = store.Hent(noegle)!;   // re-read after the upload: someone may have saved meanwhile
        h.Billeder.Add(new Billede(Guid.NewGuid().ToString("N")[..10], fil, Husstand.RenTekst(hvem, 30), clock.GetUtcNow().UtcDateTime));
        while (h.Billeder.Count > Billede.MaxPrFamilie)
        {
            var gammel = h.Billeder.OrderBy(b => b.Uploadet).First();
            lager.Slet(h.Id, gammel.Fil);
            h.Billeder.Remove(gammel);
        }
        store.Gem(h);
        return null;
    }

    /// <summary>The background job: read every waiting picture, one at a time. Returns how many were read.</summary>
    public async Task<int> LaesVentendeAsync(CancellationToken ct = default)
    {
        if (!laeser.Klar) return 0;
        var n = 0;
        foreach (var noegle in store.MedVentendeBilleder())
        {
            while (!ct.IsCancellationRequested && store.Hent(noegle) is { } h && h.Billeder.FirstOrDefault(b => b.Status == BilledStatus.Venter) is { } b)
            {
                await LaesEtAsync(noegle, h, b, ct);
                n++;
            }
        }
        return n;
    }

    private async Task LaesEtAsync(string noegle, Husstand h, Billede b, CancellationToken ct)
    {
        BilledLaesning r;
        await using (var s = lager.Aabn(h.Id, b.Fil))
        {
            if (s is null) { Opdater(noegle, b.Id, x => x with { Status = BilledStatus.Fejl, Svar = "Billedet findes ikke længere." }); return; }
            r = await laeser.LaesAsync(s, b.Fil, h.Sted, ct);
        }
        if (r.Fejl is not null)
        {
            Opdater(noegle, b.Id, x => x.Forsoeg + 1 >= Billede.MaxForsoeg
                ? x with { Status = BilledStatus.Fejl, Svar = r.Fejl, Forsoeg = x.Forsoeg + 1 }
                : x with { Forsoeg = x.Forsoeg + 1 });   // Kvittering busy/asleep: try again on the next round
            return;
        }

        if (r.Slags == "kvittering" && r.Total is > 0)
        {
            var butik = Regler.ButikFra(r.Butik);
            var fejl = indkoeb.LaegInd(noegle, r.Total.Value, butik, b.Hvem, r.Dato is { } d && d <= Idag && d >= Idag.AddDays(-62) ? d : null);
            var tekst = $"Kvittering: {Kr(r.Total.Value)} kr i {butik}" + (r.Dato is { } dd ? $", {dd:d. MMM}" : "") + (fejl is null ? " – gemt." : $" – ikke gemt: {fejl}");
            if (r.Tjek.Count == 0 && fejl is null) tekst += " Rabatterne ser rigtige ud.";
            Opdater(noegle, b.Id, x => x with { Status = BilledStatus.Laest, Slags = "kvittering", Svar = tekst, Tjek = r.Tjek });
            return;
        }
        if (r.Slags == "tilbud" && r.TilbudVare is { } vare && r.TilbudPris is { } pris)
        {
            var butik = r.TilbudButik is { } tb ? Regler.ButikFra(tb) is var g && g != "Andet" ? g : tb : null;
            var note = $"{Kr(pris)} kr" + (r.KunMedApp && r.App is { } app ? $", kun med {app}" : "") + ", fra et billede";
            var fejl = indkoeb.TilfoejVare(noegle, vare, b.Hvem, butik, note);
            Opdater(noegle, b.Id, x => x with { Status = BilledStatus.Laest, Slags = "tilbud",
                Svar = fejl is null ? $"Tilbud: {vare}, {Kr(pris)} kr{(butik is null ? "" : " i " + butik)} – sat på sedlen." : $"Tilbud: {vare} – ikke sat på sedlen: {fejl}" });
            return;
        }
        Opdater(noegle, b.Id, x => x with { Status = BilledStatus.Laest, Slags = "ukendt",
            Svar = r.Slags == "kvittering" ? "Kvittering, men totalen kunne ikke læses. Skriv beløbet under Har handlet." : "Billedet kunne ikke læses som en kvittering eller et tilbud." });
    }

    public bool Slet(string noegle, string id)
    {
        if (store.Hent(noegle) is not { } h || h.Billeder.FirstOrDefault(b => b.Id == id) is not { } b) return false;
        lager.Slet(h.Id, b.Fil);
        h.Billeder.Remove(b);
        store.Gem(h);
        return true;
    }

    /// <summary>Prices keep their øre (9,95 kr, not 10 kr) - an offer price is the point.</summary>
    private static string Kr(decimal v) => v.ToString(v % 1 == 0 ? "#,##0" : "#,##0.00", System.Globalization.CultureInfo.GetCultureInfo("da-DK"));

    private DateOnly Idag => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(clock.GetUtcNow().UtcDateTime, TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen")));

    private void Opdater(string noegle, string id, Func<Billede, Billede> f)
    {
        if (store.Hent(noegle) is not { } h) return;
        var i = h.Billeder.FindIndex(x => x.Id == id);
        if (i < 0) return;
        h.Billeder[i] = f(h.Billeder[i]);
        store.Gem(h);
    }
}
