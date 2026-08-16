
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
// GENERIC PER-CLIENT PATHS
// =========================

// One env var (MediaSettings__ClientSlug) instead of hand-editing three
// separate paths per client - switching clients is now just changing this
// one value + restarting the container, no volume-path editing needed as
// long as C:/FileSorterJobs/incoming/{slug} and .../library/{slug} exist
// under the generic /jobs and /library mounts in docker-compose.yaml.
var clientSlug = builder.Configuration["MediaSettings:ClientSlug"];
if (!string.IsNullOrWhiteSpace(clientSlug))
{
    builder.Configuration["MediaSettings:SourceRoot"] = $"/jobs/{clientSlug}";
    builder.Configuration["MediaSettings:LibraryRoot"] = $"/library/{clientSlug}";
}

// Always co-locate the media db with whatever LibraryRoot ends up being (via
// ClientSlug above, or a manual MediaSettings__LibraryRoot override for a local
// one-off run) instead of requiring ConnectionStrings__MediaDb to be set
// independently. Previously a local run pointed at a new library path silently
// kept using the generic default db from appsettings.Development.json unless
// someone remembered to override the connection string too - making a library
// that had genuinely already been indexed look uncached.
var libraryRootForDb = builder.Configuration["MediaSettings:LibraryRoot"];
if (!string.IsNullOrWhiteSpace(libraryRootForDb))
{
    builder.Configuration["ConnectionStrings:MediaDb"] = $"Data Source={Path.Combine(libraryRootForDb, ".media.db")}";
}

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
builder.Services.AddSingleton<FileSorterPushService>();

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

app.MapPost("/api/push/subscribe", async (PushSubscribeRequest req, FileSorterPushService push) =>
{
    await push.SubscribeAsync(req.Endpoint, req.P256dh, req.Auth);
    return Results.Ok();
});

app.MapPost("/api/push/unsubscribe", async (PushUnsubscribeRequest req, FileSorterPushService push) =>
{
    await push.UnsubscribeAsync(req.Endpoint);
    return Results.Ok();
});

// TEMP DEBUG - driving the Google Drive Takeout multi-batch job, removed after
app.MapPost("/api/debug/p1-start", async (string source, string output, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IPackage1Client client) =>
{
    var workflowId = await client.StartAsync(new ITMartin.Media.Contracts.Contracts.Runtime.Requests.Package1.StartPackage1Request
    {
        SourceLibraryPath = source,
        WorkingDirectory = System.IO.Path.Combine(source, ".package1"),
        OutputPath = output,
        EnableDeduplication = true,
        EnableAiClassification = false,
        EnableOcr = false,
        Profile = "Package1"
    }, CancellationToken.None);
    return Results.Ok(new { workflowId });
});

// TEMP DEBUG - reset a library back to right after Package1's own export,
// before any Package3/add-on step touched it (see Package1BaselineHelper) -
// undoes anything an add-on run has done since, so add-ons can always be
// experimented with, or re-run cleanly, without re-sorting from source.
app.MapPost("/api/debug/p1-restore-baseline", async (string path) =>
{
    var baselinePath = ITMartin.Media.Contracts.Contracts.Runtime.Helpers.Package1BaselineHelper.GetBaselinePath(path);
    if (!Directory.Exists(baselinePath))
        return Results.NotFound($"No baseline found at {baselinePath} - baseline is created automatically by the first p1-start run against this library.");

    await ITMartin.Media.Contracts.Contracts.Runtime.Helpers.Package1BaselineHelper.MirrorDirectoryAsync(baselinePath, path, CancellationToken.None);
    return Results.Ok(new { restoredFrom = baselinePath });
});

// TEMP DEBUG - polling a p1-start run's progress, removed after.
// Note: p1-start's returned "workflowId" is the background-job queue message
// id, generated in Package1Client.StartAsync BEFORE the job is even
// dequeued - it is NOT the same id IScanOrchestrator.StartAsync mints inside
// StartPackage1Handler for actual WorkflowInstances tracking (Package1WorkflowState
// carries no id field to connect the two). Rather than plumb an id through the
// whole queue/orchestrator path just for a debug endpoint, this just returns
// the most recently started Package1 run - fine for one-at-a-time local/test use.
app.MapGet("/api/debug/p1-status", async (Microsoft.EntityFrameworkCore.IDbContextFactory<ITMartin.Media.Infrastructure.Persistence.MediaDbContext> dbFactory) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var instance = await db.WorkflowInstances
        .Where(x => x.WorkflowName == "Package1Workflow")
        .OrderByDescending(x => x.StartedAtUtc)
        .FirstOrDefaultAsync();
    return instance is null
        ? Results.NotFound()
        : Results.Ok(new
        {
            instance.Status,
            instance.CurrentStep,
            instance.ProgressCurrent,
            instance.ProgressTotal,
            instance.ProgressItem,
            instance.FailureReason,
            instance.CompletedAtUtc
        });
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

// TEMP DEBUG - onboarding a new tenant's unnamed people, removed after
app.MapGet("/api/debug/p3-discover-clusters", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IPackage3Service service, Microsoft.EntityFrameworkCore.IDbContextFactory<ITMartin.Media.Infrastructure.Persistence.MediaDbContext> dbFactory) =>
{
    var clusters = await service.DiscoverUnnamedPeopleAsync(path);

    await using var db = await dbFactory.CreateDbContextAsync();
    var singleFaceFiles = (await db.MediaFaces
            .GroupBy(f => f.MediaFilePath)
            .Select(g => new { g.Key, Count = g.Count() })
            .Where(g => g.Count == 1)
            .Select(g => g.Key)
            .ToListAsync())
        .ToHashSet();

    var result = clusters.Select((c, i) => new
    {
        clusterIndex = i,
        fileCount = c.MediaFilePaths.Count,
        singleFaceSamples = c.MediaFilePaths.Where(singleFaceFiles.Contains).Take(5).ToList(),
        fallbackSample = c.SampleMediaFilePath,
        allFiles = c.MediaFilePaths
    });

    return Results.Ok(result);
});

app.MapPost("/api/debug/p3-name-cluster", async (string path, string name, List<string> clusterMediaFilePaths, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IPackage3Service service) =>
{
    var personId = await service.NamePersonFromClusterAsync(name, clusterMediaFilePaths, path);
    return Results.Ok(new { personId });
});

app.MapPost("/api/debug/test-auto-rotate", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Workflows.IImageConverterService service) =>
{
    var result = await service.ConvertToJpgAsync(path);
    return Results.Ok(new { outputPath = result });
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

app.MapPost("/api/debug/sf-person", async (string path, Guid personId, double? threshold, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ISmartFoldersService service) =>
    Results.Ok(await service.GeneratePersonFolderAsync(path, personId, threshold ?? 0.45)));

app.MapGet("/api/debug/p3-find-matches-count", async (Guid personId, double threshold, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IPackage3Service service) =>
{
    var matches = await service.FindMatchesAsync(personId, threshold);
    return Results.Ok(new { count = matches.Count });
});

app.MapPost("/api/debug/p3-classify-unhandled", async (string path, int? maxFiles, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IPackage3Service service) =>
    Results.Ok(await service.ClassifyUnhandledFilesAsync(path, maxFiles ?? 5000)));

app.MapPost("/api/debug/sf-delete-person", async (Guid personId, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IPackage3Service service) =>
{
    await service.DeletePersonAsync(personId);
    return Results.Ok("deleted");
});

app.MapPost("/api/debug/sf-yearbook", async (string path, int year, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ISmartFoldersService service) =>
    Results.Ok(await service.GenerateYearbookAsync(path, year)));

app.MapPost("/api/debug/sf-sync-collections", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ISmartFoldersService service) =>
{
    await service.SyncGalleryCollectionsAsync(path);
    return Results.Ok("synced");
});

app.MapPost("/api/debug/sf-traditions", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ISmartFoldersService service) =>
    Results.Ok(await service.GenerateTraditionsAsync(path)));

app.MapPost("/api/debug/sf-estimate-undated", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ISmartFoldersService service) =>
    Results.Ok(await service.EstimateUndatedPhotoYearsAsync(path)));

app.MapPost("/api/debug/sf-bestshot", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ISmartFoldersService service) =>
    Results.Ok(await service.PickBestShotsAsync(path)));

app.MapPost("/api/debug/sf-yearbook-captions", async (string path, int year, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ISmartFoldersService service) =>
    Results.Ok(await service.AddYearbookCaptionsAsync(path, year)));

app.MapPost("/api/debug/tag-images", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IImageTaggingService service) =>
    Results.Ok(await service.TagLibraryAsync(path)));

app.MapPost("/api/debug/p3-estimate-undated", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IPackage3Service service) =>
    Results.Ok(await service.EstimateUndatedDatesAsync(path)));

app.MapGet("/api/debug/mediafaces-paths", async (string like, Microsoft.EntityFrameworkCore.IDbContextFactory<ITMartin.Media.Infrastructure.Persistence.MediaDbContext> dbFactory) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var rows = await db.MediaFaces
        .Where(x => x.MediaFilePath.Contains(like))
        .Select(x => x.MediaFilePath)
        .ToListAsync();
    return Results.Ok(rows);
});

app.MapGet("/api/debug/mediafaces-detail", async (string like, Microsoft.EntityFrameworkCore.IDbContextFactory<ITMartin.Media.Infrastructure.Persistence.MediaDbContext> dbFactory) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var rows = await db.MediaFaces
        .Where(x => x.MediaFilePath.Contains(like))
        .Select(x => new { x.MediaFilePath, EmbeddingLength = x.EmbeddingJson.Length, x.MatchedPersonId, x.Confidence })
        .ToListAsync();
    return Results.Ok(rows);
});

// TEMP DEBUG - final delivery polish (empty folders, OS junk, hide manifest), removed after
app.MapPost("/api/debug/library-polish", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ILibraryPolishService service) =>
    Results.Ok(await service.PolishAsync(path)));

app.MapPost("/api/debug/fix-orientation", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ILibraryPolishService service) =>
    Results.Ok(await service.FixOrientationAsync(path)));

app.MapPost("/api/debug/redate-undated", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ILibraryPolishService service) =>
    Results.Ok(await service.RedateUndatedAsync(path)));

app.MapPost("/api/debug/group-by-camera", async (string path, string makeContains, string folderName, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ILibraryPolishService service) =>
    Results.Ok(await service.GroupByCameraMakeAsync(path, makeContains, folderName)));

app.MapPost("/api/debug/deduplicate-folder", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ILibraryPolishService service) =>
    Results.Ok(await service.DeduplicateFolderAsync(path)));

app.MapGet("/api/debug/camera-survey", (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IExifService exif) =>
{
    var billederDir = System.IO.Path.Combine(path, "Billeder");
    if (!System.IO.Directory.Exists(billederDir)) return Results.Ok(new { });

    var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    foreach (var file in System.IO.Directory.EnumerateFiles(billederDir, "*", System.IO.SearchOption.AllDirectories))
    {
        if (!ITMartin.Media.Infrastructure.Media.MediaTypeHelper.IsImage(file)) continue;
        (string? Make, string? Model, string? Software)? meta;
        try { meta = exif.ReadMetadata(file); } catch { continue; }
        var key = string.IsNullOrWhiteSpace(meta?.Make) && string.IsNullOrWhiteSpace(meta?.Model)
            ? "(ingen kamera-EXIF)"
            : $"{meta?.Make} {meta?.Model}".Trim();
        counts[key] = counts.GetValueOrDefault(key) + 1;
    }
    return Results.Ok(counts.OrderByDescending(kv => kv.Value).ToDictionary(kv => kv.Key, kv => kv.Value));
});

app.MapGet("/api/debug/exif-dump", (string path) =>
{
    try
    {
        var directories = MetadataExtractor.ImageMetadataReader.ReadMetadata(path);
        var dump = directories.Select(d => new
        {
            Directory = d.Name,
            Tags = d.Tags.Select(t => new { t.Name, Value = t.Description }).ToList(),
            Errors = d.Errors.ToList()
        }).ToList();
        var xmpProps = directories.OfType<MetadataExtractor.Formats.Xmp.XmpDirectory>().FirstOrDefault()?.GetXmpProperties();
        return Results.Ok(new { directories = dump, xmpProperties = xmpProps });
    }
    catch (Exception ex)
    {
        return Results.Ok(new { error = ex.ToString() });
    }
});

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

// TEMP DEBUG - cluster visually-similar undated files (same background/scene)
// into subfolders using perceptual hashing - no AI calls, reuses the
// near-duplicate infrastructure from DuplicateService but with a looser
// threshold since "similar" (not "the same photo twice") is the goal here.
// Non-recursive: only groups files directly in `path`. Videos get a poster
// frame extracted first (via IThumbnailService, same as the gallery) since
// perceptual hashing only works on decoded pixels.
app.MapPost("/api/debug/cluster-similar", (string path, int? threshold, IServiceScopeFactory scopeFactory) =>
{
    _ = Task.Run(async () =>
    {
        using var scope = scopeFactory.CreateScope();
        var pHash = scope.ServiceProvider.GetRequiredService<ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IPerceptualHashService>();
        var thumbs = scope.ServiceProvider.GetRequiredService<ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IThumbnailService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var th = threshold ?? 12;

        try
        {
            if (!Directory.Exists(path))
            {
                logger.LogError("Cluster-similar: path not found {Path}", path);
                return;
            }

            var files = Directory.EnumerateFiles(path).ToList();
            var frameDir = Path.Combine(Path.GetTempPath(), "ITMartinFileSorter", "cluster-frames");
            Directory.CreateDirectory(frameDir);

            var hashes = new List<(string Path, ulong Hash)>();
            var done = 0;

            foreach (var f in files)
            {
                done++;
                if (done % 200 == 0)
                    logger.LogInformation("Cluster-similar hashing progress: {Done}/{Total}", done, files.Count);

                var isVideo = ITMartin.Media.Infrastructure.Media.MediaTypeHelper.IsVideo(f);
                var hashInput = f;

                if (isVideo)
                {
                    var framePath = Path.Combine(frameDir, Path.GetFileNameWithoutExtension(f) + ".jpg");
                    if (!File.Exists(framePath))
                    {
                        try { await thumbs.GenerateAsync(f, framePath); }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Cluster-similar: frame extraction failed for {Path}", f);
                            continue;
                        }
                    }
                    hashInput = framePath;
                }

                var hash = await pHash.ComputeAsync(hashInput);
                if (hash is { } h) hashes.Add((f, h));
            }

            // Union-find over the hash list, threshold controls how loose
            // "similar" is (dedup uses 6; this defaults to 12 - similar
            // scene/background, not necessarily the same exact shot).
            var parent = Enumerable.Range(0, hashes.Count).ToArray();
            int Find(int x)
            {
                while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
                return x;
            }
            void Union(int a, int b)
            {
                a = Find(a); b = Find(b);
                if (a != b) parent[a] = b;
            }

            for (var i = 0; i < hashes.Count; i++)
            {
                for (var j = i + 1; j < hashes.Count; j++)
                {
                    if (pHash.HammingDistance(hashes[i].Hash, hashes[j].Hash) <= th)
                        Union(i, j);
                }
            }

            var groups = Enumerable.Range(0, hashes.Count)
                .GroupBy(Find)
                .Where(g => g.Count() > 1)
                .OrderByDescending(g => g.Count())
                .ToList();

            var groupNum = 0;
            var moved = 0;

            foreach (var group in groups)
            {
                groupNum++;
                var folder = Path.Combine(path, $"Gruppe {groupNum}");
                Directory.CreateDirectory(folder);

                foreach (var idx in group)
                {
                    var src = hashes[idx].Path;
                    var dest = Path.Combine(folder, Path.GetFileName(src));
                    var attempt = 1;
                    while (File.Exists(dest))
                    {
                        dest = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(src)}_{attempt}{Path.GetExtension(src)}");
                        attempt++;
                    }

                    try { File.Move(src, dest); moved++; }
                    catch (Exception ex) { logger.LogWarning(ex, "Cluster-similar: failed to move {Path}", src); }
                }
            }

            logger.LogInformation(
                "Cluster-similar complete for {Path}: {Total} files, {Hashed} hashed, {Groups} groups formed, {Moved} moved, {Singles} left ungrouped",
                path, files.Count, hashes.Count, groups.Count, moved, hashes.Count - moved);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cluster-similar failed for {Path}", path);
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

record PushSubscribeRequest(string Endpoint, string P256dh, string Auth);
record PushUnsubscribeRequest(string Endpoint);
