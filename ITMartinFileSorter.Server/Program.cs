
using ITMartin.Ai;
using ITMartin.Media.Infrastructure.DependencyInjection;
using ITMartinFileSorter.Server;
using ITMartinFileSorter.Server.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// =========================
// BLAZOR
// =========================

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<Microsoft.AspNetCore.Components.Server.CircuitOptions>(options =>
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

// TEMP DEBUG - driving the Google Drive Takeout multi-batch job, removed after
app.MapPost("/api/debug/p1-start", async (string source, string output, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IPackage1Client client) =>
{
    await client.StartAsync(new ITMartin.Media.Contracts.Contracts.Runtime.Requests.Package1.StartPackage1Request
    {
        SourceLibraryPath = source,
        WorkingDirectory = System.IO.Path.Combine(source, ".package1"),
        OutputPath = output,
        EnableDeduplication = true,
        EnableAiClassification = false,
        EnableOcr = false,
        Profile = "Package1"
    }, CancellationToken.None);
    return Results.Ok("started");
});

// TEMP DEBUG - testing Package3 face indexing end-to-end, removed after
app.MapPost("/api/debug/p3-index-faces", (string path, IServiceScopeFactory scopeFactory) =>
{
    // Own DI scope, independent of this HTTP request's lifetime - same pattern
    // Package3.razor uses, so the scoped DbContext factory survives the request.
    _ = Task.Run(async () =>
    {
        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IPackage3Service>();
        await service.IndexFacesAsync(path);
    });
    return Results.Ok("started");
});
app.MapGet("/api/debug/p3-status", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IPackage3Service service) =>
{
    var status = await service.GetIndexStatusAsync(path, ITMartin.Media.Contracts.Contracts.Runtime.Models.Package3IndexType.Faces);
    return Results.Ok(status);
});

// TEMP DEBUG - SmartFolders (trip/location + person + yearbook), removed after
app.MapPost("/api/debug/sf-trips", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ISmartFoldersService service) =>
    Results.Ok(await service.GenerateTripFoldersAsync(path)));

app.MapGet("/api/debug/sf-gps-stats", (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IGpsService gps) =>
{
    var files = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
        .Where(f => !f.Contains("SmartFolders", StringComparison.OrdinalIgnoreCase))
        .Where(ITMartin.Media.Contracts.Contracts.Runtime.Helpers.MediaTypeHelper.IsImage)
        .ToList();

    var withGps = 0;
    var sample = new List<object>();
    foreach (var f in files)
    {
        var coords = gps.GetCoordinates(f);
        if (coords is not null)
        {
            withGps++;
            if (sample.Count < 10) sample.Add(new { file = f, lat = coords.Value.lat, lng = coords.Value.lng });
        }
    }

    return Results.Ok(new { total = files.Count, withGps, sample });
});

app.MapPost("/api/debug/sf-add-person", async (string path, string name, string referencePhotoPath, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IPackage3Service service) =>
{
    var bytes = await File.ReadAllBytesAsync(referencePhotoPath);
    var personId = await service.AddPersonAsync(name, [new(Path.GetFileName(referencePhotoPath), bytes)], path);
    return Results.Ok(new { personId });
});

app.MapPost("/api/debug/sf-person", async (string path, Guid personId, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ISmartFoldersService service) =>
    Results.Ok(await service.GeneratePersonFolderAsync(path, personId)));

app.MapPost("/api/debug/sf-yearbook", async (string path, int year, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ISmartFoldersService service) =>
    Results.Ok(await service.GenerateYearbookAsync(path, year)));

app.MapPost("/api/debug/sf-homeaway", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ISmartFoldersService service) =>
    Results.Ok(await service.GenerateHomeAwayFoldersAsync(path)));

app.MapPost("/api/debug/sf-sync-collections", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ISmartFoldersService service) =>
{
    await service.SyncGalleryCollectionsAsync(path);
    return Results.Ok("synced");
});

app.MapGet("/api/debug/mediafaces-paths", async (string like, Microsoft.EntityFrameworkCore.IDbContextFactory<ITMartin.Media.Infrastructure.Persistence.MediaDbContext> dbFactory) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var rows = await db.MediaFaces
        .Where(x => x.MediaFilePath.Contains(like))
        .Select(x => x.MediaFilePath)
        .ToListAsync();
    return Results.Ok(rows);
});

// TEMP DEBUG - final delivery polish (empty folders, OS junk, hide manifest), removed after
app.MapPost("/api/debug/library-polish", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ILibraryPolishService service) =>
    Results.Ok(await service.PolishAsync(path)));

// TEMP DEBUG - static offline gallery export (thumbnails + HTML pages), removed after
app.MapPost("/api/debug/gallery-export", (string path, IServiceScopeFactory scopeFactory) =>
{
    _ = Task.Run(async () =>
    {
        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IStaticGalleryExportService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        try
        {
            var result = await service.ExportAsync(path);
            logger.LogInformation("Gallery export finished: {Total} files, {Generated} new thumbnails, {Years} years", result.TotalFiles, result.ThumbnailsGenerated, result.YearsGenerated);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Gallery export failed for {Path}", path);
        }
    });
    return Results.Ok("started");
});

// TEMP DEBUG - per-folder gallery grid thumbnails (fixes slow-loading live gallery), removed after
app.MapPost("/api/debug/gallery-thumbnails", (string path, IServiceScopeFactory scopeFactory) =>
{
    _ = Task.Run(async () =>
    {
        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IGalleryThumbnailService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        try
        {
            var generated = await service.GenerateAsync(path);
            logger.LogInformation("Gallery thumbnail generation finished: {Generated} new thumbnails", generated);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Gallery thumbnail generation failed for {Path}", path);
        }
    });
    return Results.Ok("started");
});

app.MapControllers();

app.MapMediaSignalRHubs();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// =========================
// RUN
// =========================

app.Run();
