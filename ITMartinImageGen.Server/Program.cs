using ITMartinImageGen.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddHttpClient();

builder.Services.AddSingleton<FalAiService>();
builder.Services.AddSingleton<ClaudePromptService>();
builder.Services.AddSingleton<ImageStorageService>();

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

app.MapRazorComponents<ITMartinImageGen.Server.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
