
using ITMartin.Ai;
using ITMartin.Media.Infrastructure.DependencyInjection;
using ITMartinFileSorter.Server;
using ITMartinFileSorter.Server.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// =========================
// BLAZOR
// =========================

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services
    .AddServerSideBlazor()
    .AddCircuitOptions(options =>
    {
        options.DetailedErrors = true;
    });

// =========================
// SERVICES
// =========================

builder.Services.AddMediaInfrastructureCore(builder.Configuration);
builder.Services.AddFileSorterCore();
builder.Services.AddFileSorterServer();
builder.Services.AddAi();
builder.Services.AddSingleton<ToastService>();

// =========================
// SIGNALR (after Core so SignalR publisher overrides the null default)
// =========================

builder.Services.AddMediaSignalR();

// =========================
// LOGGING
// =========================

builder.Logging.ClearProviders();

builder.Logging.AddConsole();

builder.Logging.AddFilter(
    "Microsoft.EntityFrameworkCore",
    LogLevel.None);

builder.Logging.AddFilter(
    "Microsoft.EntityFrameworkCore.Database.Command",
    LogLevel.None);

// =========================
// HTTP CLIENT
// =========================

builder.Services.AddScoped(sp =>
{
    var navigation =
        sp.GetRequiredService<
            NavigationManager>();

    return new HttpClient
    {
        BaseAddress =
            new Uri(
                navigation.BaseUri)
    };
});

// =========================
// CONTROLLERS
// =========================

builder.Services.AddControllers();

// =========================
// BUILD
// =========================

var app = builder.Build();

// =========================
// ERROR HANDLING
// =========================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(
        "/Error");
}

// =========================
// STATIC FILES
// =========================

app.UseStaticFiles();

// =========================
// LIBRARY FILES
// =========================

var libraryPath =
    builder.Configuration[
        "MediaSettings:LibraryRoot"];

var provider =
    new FileExtensionContentTypeProvider();

provider.Mappings[".mp4"] =
    "video/mp4";

provider.Mappings[".mov"] =
    "video/quicktime";

provider.Mappings[".mkv"] =
    "video/x-matroska";

provider.Mappings[".jpg"] =
    "image/jpeg";

provider.Mappings[".jpeg"] =
    "image/jpeg";

provider.Mappings[".png"] =
    "image/png";

provider.Mappings[".webp"] =
    "image/webp";

provider.Mappings[".gif"] =
    "image/gif";

provider.Mappings[".heic"] =
    "image/heic";

provider.Mappings[".avif"] =
    "image/avif";

if (!string.IsNullOrWhiteSpace(
        libraryPath) &&
    Directory.Exists(
        libraryPath))
{
    app.UseStaticFiles(
        new StaticFileOptions
        {
            FileProvider =
                new PhysicalFileProvider(
                    libraryPath),

            RequestPath =
                "/libraryfiles",

            ContentTypeProvider =
                provider
        });
}

// =========================
// SOURCE FILES
// =========================

var sourcePath =
    builder.Configuration[
        "MediaSettings:SourceRoot"];

if (!string.IsNullOrWhiteSpace(sourcePath) &&
    Directory.Exists(sourcePath))
{
    app.UseStaticFiles(
        new StaticFileOptions
        {
            FileProvider =
                new PhysicalFileProvider(sourcePath),

            RequestPath = "/sourcefiles",

            ContentTypeProvider = provider,

            ServeUnknownFileTypes = false
        });
}

// =========================
// PIPELINE
// =========================

app.UseAntiforgery();

app.MapControllers();

app.MapMediaSignalRHubs();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// =========================
// RUN
// =========================

app.Run();
