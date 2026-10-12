using ITMartinAdhd.Server;
using ITMartin.Shared.UI.Kolibri;
using ITMartinAdhd.Application;
using ITMartinAdhd.Infrastructure;
using ITMartinAdhd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Environment.IsDevelopment()
    ? builder.Configuration.GetConnectionString("AdhdDb")
    : "Data Source=/app/data/adhd.db";

builder.Services.AddAdhdApplication();
builder.Services.AddAdhdInfrastructure(builder.Configuration, connectionString ?? "Data Source=adhd.db");
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);

// Kolibri contract (/health, manifest, /om, /hjaelp); the page is the static wwwroot/index.html.
builder.Services.AddKolibri(k =>
{
    k.Name = "Find det igen";
    k.KolibriName = "Kolibri Hukommelse";
    k.Family = "hukommelse";
    k.Tagline = "Skriv ned, hvor du lægger tingene – så finder du dem igen med ét søgeord.";
    k.About =
    [
        "Find det igen er til nøgler, briller, pas og opladere – alt det, der forsvinder lige når man skal bruge det.",
        "Når du lægger noget fra dig, skriver du hvad det er og hvor det ligger – gerne med et billede. Når du leder, søger du bare.",
    ];
    k.HowTo =
    [
        "Tryk Læg noget fra dig, skriv tingen og stedet, og tag evt. et billede. Tryk Gem.",
        "Leder du? Skriv et ord i søgefeltet – fx nøgler eller skuffe.",
        "Tryk på en ting for at se billedet, flytte den eller slette den.",
    ];
    k.Version = "2026.10";
    k.ThemeColor = "#f4f6f5";
    k.StaticPages = true;
});

var app = builder.Build();
// Owner only until a free version keeps things in the visitor's own browser (PinGate.cs).
app.UseFindItPinGate();
app.MapKolibri();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AdhdDbContext>();
    db.Database.Migrate();
    if (app.Configuration.GetValue<bool>("Adhd:SeedDemoData"))
        await DemoSeeder.SeedAsync(db);
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/items/recent", (Things things, CancellationToken ct) => things.RecentAsync(ct));
app.MapGet("/api/items/search", (string? q, Things things, CancellationToken ct) => things.SearchAsync(q, ct));

app.MapPost("/api/items", async (HttpRequest req, Things things, CancellationToken ct) =>
{
    var form = await req.ReadFormAsync(ct);
    var photo = form.Files.GetFile("photo");
    await using var stream = photo is { Length: > 0 } ? photo.OpenReadStream() : null;
    var item = await things.PutDownAsync(form["name"], form["location"], form["notes"], stream, photo?.FileName, ct);
    return item is null ? Results.BadRequest("Skriv både hvad det er, og hvor det ligger.") : Results.Ok(item);
}).DisableAntiforgery();

app.MapPut("/api/items/{id:int}/location", async (int id, HttpRequest req, Things things, CancellationToken ct) =>
{
    var form = await req.ReadFormAsync(ct);
    return await things.MoveAsync(id, form["location"], ct) ? Results.Ok() : Results.BadRequest();
}).DisableAntiforgery();

app.MapDelete("/api/items/{id:int}", async (int id, Things things, CancellationToken ct) =>
    await things.DeleteAsync(id, ct) ? Results.Ok() : Results.NotFound());

app.MapGet("/photos/{id:int}", async (int id, Things things, CancellationToken ct) =>
{
    if (await things.PhotoFileAsync(id, ct) is not { } full) return Results.NotFound();
    var mime = Path.GetExtension(full).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".heic" => "image/heic",
        _ => "image/jpeg",
    };
    return Results.File(full, mime);
});

app.Run();
