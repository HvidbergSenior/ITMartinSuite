using ITMartin.Shared.UI.Kolibri;
using ITMartinDailyBrief.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKolibri(k =>
{
    k.Name = "Nyheder";
    k.KolibriName = "Kolibri Kommunikere";
    k.Family = "kommunikere";
    k.Tagline = "De nyeste nyheder fra de kilder, du selv vælger – uden reklamer, og med en ende.";
    k.About =
    [
        "Nyheder samler det nyeste fra de kilder, du selv vælger (TV 2, DR, Politiken …), ét sted – nyeste først.",
        "Listen har en ende: du vælger 5, 10 eller 15 nyheder, og så er du opdateret. Ingen reklamer, ingen uendelig scroll.",
        "Tryk på en nyhed for at læse den – og få et kort resumé lavet af AI, hvis du vil.",
    ];
    k.HowTo =
    [
        "Åbn siden – forsiden er de nyeste nyheder fra dine kilder.",
        "Tryk Kilder for at slå kilder til og fra, vælge antal eller tilføje din egen (en RSS-adresse). Valget gemmes kun på din egen telefon.",
        "Tryk på en nyhed og derefter Lav resumé for at få den kort fortalt.",
    ];
    k.Version = "2026.10";
    k.ThemeColor = "#f4f6f5";
});

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddDailyBriefInfrastructure(builder.Configuration);

var app = builder.Build();
app.MapKolibri();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<ITMartinDailyBrief.Server.App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();
