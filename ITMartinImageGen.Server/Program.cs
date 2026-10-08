using ITMartinImageGen.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton<FalAiService>();
builder.Services.AddSingleton<ClaudePromptService>();
builder.Services.AddSingleton<ImageStorageService>();
builder.Services.AddSingleton<UsageLimiterService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error", createScopeForErrors: true);

app.UseStaticFiles();
app.UseAntiforgery();

// Serve saved images
app.MapGet("/saved/{fileName}", (string fileName, IConfiguration config) =>
{
    var root = config["ImageStorage:Root"] ?? "/app/data/images";
    var path = Path.Combine(root, Path.GetFileName(fileName));
    if (!File.Exists(path)) return Results.NotFound();
    return Results.File(path, "image/jpeg");
});

// "⬇ Hent" for every version (user 2026-10-08). fal.ai's image links are on another host, where the browser ignores
// <a download> and just opens the picture - so the file is fetched here and sent back as a download. Only fal.ai's own
// image hosts are allowed (never an open proxy).
app.MapGet("/hent", async (string u, string? n, IHttpClientFactory http) =>
{
    if (!Uri.TryCreate(u, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
        !(uri.Host == "fal.media" || uri.Host.EndsWith(".fal.media")))
        return Results.BadRequest();
    using var client = http.CreateClient();
    client.Timeout = TimeSpan.FromSeconds(30);
    var res = await client.GetAsync(uri);
    if (!res.IsSuccessStatusCode) return Results.NotFound();
    var bytes = await res.Content.ReadAsByteArrayAsync();
    var type = res.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
    var ext = type.Contains("png") ? ".png" : type.Contains("webp") ? ".webp" : type.Contains("mp4") ? ".mp4" : ".jpg";
    var name = string.Concat((n ?? "billede").Where(char.IsLetterOrDigit).Take(40));
    return Results.File(bytes, type, (name.Length > 0 ? name : "billede") + ext);
});

app.MapRazorComponents<ITMartinImageGen.Server.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
