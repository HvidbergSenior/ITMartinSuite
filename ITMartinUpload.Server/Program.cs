using ITMartin.Shared.UI.Kolibri;
using ITMartinUpload.Application;
using ITMartinUpload.Domain;
using ITMartinUpload.Infrastructure;
using Microsoft.AspNetCore.Http.Features;

const long tenGigabytes = 10L * 1024 * 1024 * 1024;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKolibri(k =>
{
    k.Name = "Upload";
    k.KolibriName = "ITKolibri · Nektar";
    k.Family = "nektar";
    k.Icon = "📤";
    k.Tagline = "Send dine billeder til Martin – træk dem ind, så er de her.";
    k.About =
    [
        "Her sender du dine billeder, film og dokumenter direkte til Martins server i Danmark – uden at gå gennem en cloud-tjeneste eller en mail, der ikke kan klare vedhæftningen.",
        "Du får dit eget link. Det, du sender, ligger i din egen mappe, og det er kun Martin og dig, der har adgang til den.",
        "Når filerne er kommet frem, sorterer Martin dem: dubletter væk, billederne ordnet efter år og begivenhed, og til sidst et privat galleri til jer.",
    ];
    k.HowTo =
    [
        "Træk filerne ind i den store boks – eller tryk på knappen og vælg dem på telefonen eller computeren.",
        "Lad siden være åben, mens den blå streg kører. Du kan sagtens sende mange filer ad gangen.",
        "Kom bare igen senere – alt, du har sendt, står nederst på siden.",
    ];
    k.Version = "2026.10.28";
    k.ThemeColor = "#121917";
});

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = tenGigabytes);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = tenGigabytes);

// UploadRoot stays supported so the existing compose file keeps working.
builder.Services.Configure<StorageOptions>(options =>
{
    builder.Configuration.GetSection("Upload").Bind(options);
    options.Root = builder.Configuration["UploadRoot"] ?? options.Root;
});
var customers = new CustomerBook();
builder.Configuration.GetSection("Upload").Bind(customers);
builder.Configuration.GetSection("Customers").Bind(customers.Customers);
builder.Services.AddSingleton(customers);
builder.Services.AddUpload();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    // A big file list from the page (1,900 photos at once, 2026-10-26) must not break the connection; upload.js also
    // sends it in parts of 100.
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 1024 * 1024);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();
app.MapKolibri();

app.MapPost("/api/upload/{slug}", async (
    string slug, string? token, HttpRequest request, CustomerFiles files, CancellationToken ct) =>
{
    if (!customers.IsAuthorized(slug, token)) return Results.Unauthorized();
    if (!UploadNames.IsValidSlug(slug)) return Results.BadRequest("Ugyldigt navn");
    if (!request.HasFormContentType) return Results.BadRequest("Forkert format");

    var form = await request.ReadFormAsync(ct);
    var file = form.Files.FirstOrDefault();
    if (file is null || file.Length == 0) return Results.BadRequest("Ingen fil modtaget");

    await using var content = file.OpenReadStream();
    return await files.ReceiveAsync(slug, token, file.FileName, form["path"], file.Length, content, ct) switch
    {
        ReceiveResult.Unauthorized => Results.Unauthorized(),
        ReceiveResult.BadSlug => Results.BadRequest("Ugyldigt navn"),
        ReceiveResult.BadFileName => Results.BadRequest("Ugyldigt filnavn"),
        _ => Results.Ok(),
    };
}).DisableAntiforgery();

// Push sign-up. A customer may only subscribe to their own thread, and only with
// the token from their link; "martin" is behind the admin pin.
app.MapPost("/api/push/{who}", (
    string who, string? token, string? pin, PushRegistration registration, Conversations chat, IConfiguration config) =>
{
    var pinOk = !string.IsNullOrEmpty(config["Upload:AdminPin"]) && pin == config["Upload:AdminPin"];
    return chat.Subscribe(who, token, pinOk, registration) ? Results.Ok() : Results.Unauthorized();
}).DisableAntiforgery();

app.MapGet("/api/push-key", (Conversations chat) => Results.Text(chat.PushKey));

// The customer's own files, so they can see what they sent and take something back out.
app.MapGet("/api/file/{slug}/{name}", (string slug, string name, string? token, bool? thumb, CustomerFiles files) =>
{
    if (!customers.IsAuthorized(slug, token)) return Results.Unauthorized();
    var file = files.Open(slug, token, name, thumb == true);
    if (file is null) return Results.NotFound();
    return file.Thumbnail is { } jpeg
        ? Results.File(jpeg, "image/jpeg")
        : Results.File(file.Path, file.ContentType, enableRangeProcessing: true);
});

app.MapPost("/api/remove/{slug}/{name}", (string slug, string name, string? token, CustomerFiles files) =>
{
    if (!customers.IsAuthorized(slug, token)) return Results.Unauthorized();
    return files.Remove(slug, token, name) ? Results.Ok() : Results.NotFound();
}).DisableAntiforgery();

// Home-screen app for one event: its own name and start page, the camera icon.
app.MapGet("/event/{slug}/manifest.webmanifest", (string slug, string? token) =>
{
    var c = customers.Find(slug) ?? customers.Renamed(slug);
    if (c is null || !UploadNames.IsValidSlug(slug)) return Results.NotFound();
    slug = c.Slug;
    var name = string.IsNullOrWhiteSpace(c.Name) ? slug : c.Name;
    var manifest = new
    {
        name = $"{name} – billeder",
        short_name = name,
        // A right code is kept in the start page, so the home-screen icon opens the event without "Adgang nægtet";
        // a wrong or missing one is left out (never echoed back).
        start_url = $"/event/{slug}" + (!string.IsNullOrEmpty(token) && customers.IsAuthorized(slug, token) ? "?token=" + Uri.EscapeDataString(token) : ""),
        scope = $"/event/{slug}",
        display = "standalone",
        background_color = "#121917",
        theme_color = "#1f7a5c",
        icons = new[]
        {
            new { src = "/event-icon-192.png", sizes = "192x192", type = "image/png", purpose = "any" },
            new { src = "/event-icon-512.png", sizes = "512x512", type = "image/png", purpose = "any" },
        },
    };
    return Results.Json(manifest, contentType: "application/manifest+json");
});

app.MapRazorComponents<ITMartinUpload.Server.App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly);

app.Run();

public partial class Program;
