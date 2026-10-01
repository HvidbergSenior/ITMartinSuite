using ITMartin.Shared.UI.Kolibri;
using ITMartinNotatskriver.Server;
using ITMartinNotatskriver.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKolibri(k =>
{
    k.Name = "Notatskriver";
    k.KolibriName = "Kolibri Kommunikere";
    k.Family = "kommunikere";
    k.Tagline = "Fra stikord, diktat og billeder af håndskrift til færdigt dokument.";
    k.About =
    [
        "Notatskriver laver dine egne noter om til et færdigt dokument: mødereferat, brev, mail, rapport, ansøgning, opslag og meget mere.",
        "Du kan skrive stikord, tale dem ind, eller tage et billede af dine håndskrevne noter med telefonen. AI'en (Claude) skriver dokumentet – den bruger kun det, der står i dine noter, og sætter [?] ved det, den ikke kunne læse.",
        "Intet gemmes. Noter, billeder og dokument forsvinder, når du lukker siden. Skriv ikke følsomme oplysninger om andre (fx patienter) – til det findes Notatskriver til Windows, hvor AI'en kører på din egen PC.",
    ];
    k.HowTo =
    [
        "Skriv stikord, tryk 🎤 og tal, eller tryk 📷 og tag billeder af dine noter.",
        "Vælg hvad du vil have: referat, brev, mail …",
        "Tryk ✨ Skriv dokumentet, læs det igennem, og kopiér, hent som Word eller del det.",
    ];
    k.Version = "2026.10";
    k.ThemeColor = "#f4f6f5";
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 32 * 1024 * 1024);

builder.Services.AddSingleton<AiBudget>();
builder.Services.AddSingleton<NoteAi>();

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
