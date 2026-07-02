using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = 10L * 1024 * 1024 * 1024);

builder.Services.Configure<FormOptions>(options =>
    options.MultipartBodyLengthLimit = 10L * 1024 * 1024 * 1024);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

var uploadRoot = builder.Configuration["UploadRoot"] ?? "/app/data";
Directory.CreateDirectory(uploadRoot);

app.MapPost("/api/upload/{slug}", async (string slug, HttpRequest request) =>
{
    if (!IsValidSlug(slug)) return Results.BadRequest("Ugyldigt navn");

    var folder = Path.Combine(uploadRoot, slug.ToLowerInvariant());
    Directory.CreateDirectory(folder);

    if (!request.HasFormContentType)
        return Results.BadRequest("Forkert format");

    var form = await request.ReadFormAsync();
    var file = form.Files.FirstOrDefault();
    if (file == null || file.Length == 0)
        return Results.BadRequest("Ingen fil modtaget");

    var safeName = Path.GetFileName(file.FileName);
    if (string.IsNullOrEmpty(safeName))
        return Results.BadRequest("Ugyldigt filnavn");

    var dest = Path.Combine(folder, safeName);
    var baseName = Path.GetFileNameWithoutExtension(safeName);
    var ext = Path.GetExtension(safeName);
    var n = 1;
    while (File.Exists(dest))
        dest = Path.Combine(folder, $"{baseName}_{n++}{ext}");

    await using var stream = File.Create(dest);
    await file.OpenReadStream().CopyToAsync(stream);

    return Results.Ok();
}).DisableAntiforgery();

app.MapRazorComponents<ITMartinUpload.Server.App>()
    .AddInteractiveServerRenderMode();

app.Run();

static bool IsValidSlug(string slug) =>
    !string.IsNullOrEmpty(slug) &&
    slug.Length <= 50 &&
    slug.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
