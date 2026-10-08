using System.Collections.Concurrent;
using ITMartinHjem.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace ITMartinHjem.Server.Services;

// Is each app on "Mine apps" running? Checked every 2 minutes from here, so a visitor never
// waits for it and never sees a Cloudflare 502. 5xx or no answer = sleeping; anything else = up
// (Upload answers 404 on its bare front page and is still up).
public sealed class AppStatus(IDbContextFactory<HjemDb> dbf, IHttpClientFactory http, ILogger<AppStatus> log) : BackgroundService
{
    private readonly ConcurrentDictionary<string, bool> _up = new();

    public bool? IsUp(string url) => _up.TryGetValue(url, out var up) ? up : null;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(2));
        do
        {
            try
            {
                await using var db = await dbf.CreateDbContextAsync(ct);
                var urls = await db.AppLinks.AsNoTracking().Where(a => a.Url != "").Select(a => a.Url).Distinct().ToListAsync(ct);
                var client = http.CreateClient("status");
                await Parallel.ForEachAsync(urls, new ParallelOptions { MaxDegreeOfParallelism = 6, CancellationToken = ct }, async (url, c) =>
                {
                    try
                    {
                        using var res = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, c);
                        _up[url] = (int)res.StatusCode < 500;
                    }
                    catch { _up[url] = false; }
                });
            }
            catch (Exception ex) when (!ct.IsCancellationRequested) { log.LogWarning(ex, "App status check failed"); }
        } while (await timer.WaitForNextTickAsync(ct));
    }

    // First start: the apps that have a public address (texts from Kontrol's apps.json).
    public static async Task SeedAsync(HjemDb db)
    {
        if (await db.AppLinks.AnyAsync()) return;
        (string Icon, string Name, string Url, string Desc, bool Show)[] seed =
        [
            ("⚡", "ElPriser", "https://elpriser.itmartin.dk", "Gratis elpriser time for time, hvad maskinerne koster, og push når strømmen er billig.", true),
            ("🔌", "MinElpris", "https://minelpris.itmartin.dk", "Dit eget forbrug fra måleren, apparater og regningen forklaret linje for linje.", true),
            ("🧭", "Forløbet", "https://forloebet.itmartin.dk", "Sags-hjælper: hvad gælder, hvad behøver du ikke, hvad gør du nu.", true),
            ("🩺", "PC-tjek", "https://pctjek.itmartin.dk", "Tjek af PC, mikrofon og kamera – med 'Hvad er galt?'.", true),
            ("🗳️", "Stem", "https://stem.itmartin.dk", "Afstemninger – fx logo-afstemningen.", true),
            ("🪑", "Polstrer-tavlen", "https://polstrertavle.itmartin.dk", "Fælles tavle, hvor polstrere viser deres møbler frem.", true),
            ("🎨", "Billedbehandling", "https://billedbehandling.itmartin.dk", "Lav og redigér billeder med AI.", true),
            ("🔨", "Auktion", "https://auktion.itmartin.dk", "Live-auktion med bud i realtid.", true),
            ("🔎", "Find det igen", "https://finddetigen.itmartin.dk", "Find dine ting igen – hjælp til fokus og hukommelse.", true),
            ("📰", "Nyheder", "https://nyheder.itmartin.dk", "Dagens nyheder samlet kort.", true),
            ("🎯", "R6 Assistant", "https://r6.itmartin.dk", "Rainbow Six: kort, bans, operatører og coach-mode til kampene.", true),
            ("🚀", "Strategi", "https://strategi.itmartin.dk", "Strategi-guides til spil (GalCiv4) – på engelsk.", true),
            ("🖼️", "Galleri", "https://gallery.itmartin.dk", "Familiens og kundernes fotogallerier – og fødselsdagssiderne.", false),
            ("📤", "Upload", "https://upload.itmartin.dk", "Kunder og gæster lægger billeder direkte ind (fx Polstrermesse).", false),
            ("🛋️", "Polstrer", "https://polstrer.itmartin.dk", "Polstrerens procesjournal: tag billede, erfaringsarkiv og AI-forslag.", false),
            ("💿", "Bibliotek", "https://bibliotek.itmartin.dk", "Rippede film og musik, kategorier og rip-station-status.", false),
            ("🎬", "Jellyfin", "https://jellyfin.itmartin.dk", "Afspiller film og musik fra biblioteket på TV, telefon og PC.", false),
            ("🎛️", "Kontrol", "https://kontrol.itmartin.dk", "Tænd og sluk alle apps, se forbrug og backup.", false),
        ];
        var i = 0;
        foreach (var s in seed)
            db.AppLinks.Add(new AppLink { Icon = s.Icon, Name = s.Name, Url = s.Url, Description = s.Desc, Show = s.Show, Sort = (++i) * 10 });
        await db.SaveChangesAsync();
    }
}
