using ITMartin.Shared.UI.Kolibri;
using ITMartinMusicCheck.Server;
using ITMartinMusicCheck.Server.Services;

// Mine skiver (Kolibri Rede) - replaced Musik-tjek 2026-10-02 (user: "make it usable for this purpose"):
// people with boxes of CDs/DVDs/Blu-rays get them in use again - legally. Catalogue in the browser,
// "where can I watch it" for films (TMDB watch providers = JustWatch data), CDs -> the free Rip CD
// download, and plain rules (ophavsretsloven §12 + §75c). No DVD ripping anywhere, by design.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKolibri(k =>
{
    k.Name = "Mine skiver";
    k.KolibriName = "Kolibri Rede";
    k.Family = "rede";
    k.Tagline = "Få dine CD'er, DVD'er og Blu-rays i brug igen – lovligt.";
    k.About =
    [
        "Mine skiver hjælper dig med at bruge den samling, du allerede ejer: CD'er, DVD'er og Blu-rays, der står i kasser og på hylder.",
        "Du skriver dine skiver ind og ser med det samme, hvor du har dem. For hver film kan du se, om den ligger på en streamingtjeneste i Danmark – så kan du se den med ét tryk uden at finde skiven frem.",
        "Dine CD'er kan du selv lægge over på telefonen og computeren med det gratis program Rip CD. DVD og Blu-ray må man ikke kopiere i Danmark (de har kopispærre) – det forklarer siden 'Hvad må jeg?'.",
        "Din samling gemmes kun i din egen browser. Intet sendes til ITMartin. Film-data kommer fra TMDB, og hvor filmen kan ses, kommer fra JustWatch.",
    ];
    k.HowTo =
    [
        "Tryk ➕ Tilføj, vælg CD, DVD eller Blu-ray, og skriv titlen. Tryk på den rigtige i listen.",
        "Skriv evt. hvor skiven står (fx 'Kasse 3'), så du kan finde den igen.",
        "Tryk 📺 Hvor kan jeg se den? ved en film – eller 💿 Rip den selv ved en CD.",
        "Vil du fjerne noget igen, trykker du 🗑️.",
    ];
    k.Version = "2026.10";
    k.ThemeColor = "#f4f6f5";
});

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddHttpClient("tmdb", c => c.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddHttpClient("mb", c =>
{
    c.Timeout = TimeSpan.FromSeconds(15);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("ITMartinMineSkiver/1.0 (ITMartin@Mensa.dk)");   // MusicBrainz wants a contact
});
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<DiscLookup>();

var app = builder.Build();
app.MapKolibri();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();
