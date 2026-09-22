using ITMartin.Shared.UI.Kolibri;
using ITMartinUpload.Server.Services;
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
    k.Version = "2026.09";
    k.ThemeColor = "#121917";
});

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = tenGigabytes);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = tenGigabytes);

// UploadRoot stays supported so the existing compose file keeps working.
builder.Services.Configure<UploadOptions>(options =>
{
    builder.Configuration.GetSection("Upload").Bind(options);
    builder.Configuration.GetSection("Customers").Bind(options.Customers);
    options.Root = builder.Configuration["UploadRoot"] ?? options.Root;
});
builder.Services.AddSingleton<UploadStore>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();
app.MapKolibri();

app.MapPost("/api/upload/{slug}", async (
    string slug,
    string? token,
    HttpRequest request,
    UploadStore store,
    Microsoft.Extensions.Options.IOptions<UploadOptions> options,
    CancellationToken ct) =>
{
    if (!options.Value.IsAuthorized(slug, token)) return Results.Unauthorized();
    if (!UploadStore.IsValidSlug(slug)) return Results.BadRequest("Ugyldigt navn");
    if (!request.HasFormContentType) return Results.BadRequest("Forkert format");

    var form = await request.ReadFormAsync(ct);
    var file = form.Files.FirstOrDefault();
    if (file is null || file.Length == 0) return Results.BadRequest("Ingen fil modtaget");

    var safeName = UploadStore.SafeFileName(file.FileName);
    if (safeName is null) return Results.BadRequest("Ugyldigt filnavn");

    await store.SaveAsync(slug, safeName, file.OpenReadStream(), ct);
    return Results.Ok();
}).DisableAntiforgery();

app.MapRazorComponents<ITMartinUpload.Server.App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly);

app.Run();

public partial class Program;
