
using ITMartin.Ai;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Infrastructure.DependencyInjection;
using ITMartin.Media.Infrastructure.Persistence.Stores;
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
builder.Services.AddScoped<ToastService>();
builder.Services.AddSingleton<FileSorterPushService>();
builder.Services.AddScoped<IWorkflowAlertNotifier, DbWorkflowAlertNotifier>();
builder.Services.AddHostedService<WorkflowAlertPushHostedService>();

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

// FaceIndex Studio can browse folders outside the library/source roots (e.g. a
// standalone test folder like C:\BertilTest) - /libraryfiles and /sourcefiles
// only cover their own configured roots, so this serves an absolute path directly.
app.MapGet(
    "/localfile",
    (string path) =>
    {
        if (!File.Exists(path))
        {
            return Results.NotFound();
        }

        var contentType =
            provider.TryGetContentType(path, out var type)
                ? type
                : "application/octet-stream";

        return Results.File(path, contentType, enableRangeProcessing: true);
    });

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
app.MapPost("/api/debug/p1-start", async (string source, string output, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IQuickSortClient client, bool enableDeduplication = true, bool enableBaselineSnapshot = true) =>
{
    var workflowId = await client.StartAsync(new ITMartin.Media.Contracts.Contracts.Runtime.Requests.QuickSort.StartQuickSortRequest
    {
        SourceLibraryPath = source,
        WorkingDirectory = System.IO.Path.Combine(source, ".package1"),
        OutputPath = output,
        EnableDeduplication = enableDeduplication,
        EnableBaselineSnapshot = enableBaselineSnapshot,
        EnableAiClassification = false,
        EnableOcr = false,
        Profile = "QuickSort"
    }, CancellationToken.None);
    return Results.Ok(new { workflowId });
});

// TEMP DEBUG - reset a library back to right after QuickSort's own export,
// before any FaceIndex/add-on step touched it (see QuickSortBaselineHelper) -
// undoes anything an add-on run has done since, so add-ons can always be
// experimented with, or re-run cleanly, without re-sorting from source.
app.MapPost("/api/debug/p1-restore-baseline", async (string path) =>
{
    var baselinePath = ITMartin.Media.Contracts.Contracts.Runtime.Helpers.QuickSortBaselineHelper.GetBaselinePath(path);
    if (!Directory.Exists(baselinePath))
        return Results.NotFound($"No baseline found at {baselinePath} - baseline is created automatically by the first p1-start run against this library.");

    await ITMartin.Media.Contracts.Contracts.Runtime.Helpers.QuickSortBaselineHelper.MirrorDirectoryAsync(baselinePath, path, CancellationToken.None);
    return Results.Ok(new { restoredFrom = baselinePath });
});

// TEMP DEBUG - polling a p1-start run's progress, removed after.
// Note: p1-start's returned "workflowId" is the background-job queue message
// id, generated in QuickSortClient.StartAsync BEFORE the job is even
// dequeued - it is NOT the same id IScanOrchestrator.StartAsync mints inside
// StartQuickSortHandler for actual WorkflowInstances tracking (QuickSortWorkflowState
// carries no id field to connect the two). Rather than plumb an id through the
// whole queue/orchestrator path just for a debug endpoint, this just returns
// the most recently started QuickSort run - fine for one-at-a-time local/test use.
app.MapGet("/api/debug/p1-status", async (Microsoft.EntityFrameworkCore.IDbContextFactory<ITMartin.Media.Infrastructure.Persistence.MediaDbContext> dbFactory) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var instance = await db.WorkflowInstances
        .Where(x => x.WorkflowName == "QuickSortWorkflow")
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

// Follow-up pass for whatever QuickSort deferred (files >150MB or under a
// Film/Movies/TV/Series folder - see VideoBatchService.ShouldDefer). Same
// fire-and-forget + own-DI-scope pattern as p3-index-faces below.
app.MapPost("/api/debug/largevideoconvert-start", (string path, IServiceScopeFactory scopeFactory) =>
{
    _ = Task.Run(async () =>
    {
        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ILargeVideoConvertService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        try
        {
            await service.ConvertDeferredVideosAsync(path);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LargeVideoConvert failed for {Path}", path);
        }
    });
    return Results.Ok("started");
});
app.MapGet("/api/debug/largevideoconvert-status", async (Microsoft.EntityFrameworkCore.IDbContextFactory<ITMartin.Media.Infrastructure.Persistence.MediaDbContext> dbFactory) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var instance = await db.WorkflowInstances
        .Where(x => x.WorkflowName == "LargeVideoConvertWorkflow")
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

// Package4's video-enhancement client (social/vlog clip editing) belongs to
// ITMartinVlog.Server, which has its own proper service layer around this
// pipeline (VlogEditorService/VlogFfmpegService) - FileSorter never had a
// real use for it. The p4-start/p4-status debug endpoints that used to live
// here were leftover scaffolding from before that separation, removed.

// TEMP DEBUG - testing FaceIndex face indexing end-to-end, removed after
app.MapPost("/api/debug/p3-index-faces", (string path, IServiceScopeFactory scopeFactory) =>
{
    // Own DI scope, independent of this HTTP request's lifetime - same pattern
    // FaceIndex.razor uses, so the scoped DbContext factory survives the request.
    _ = Task.Run(async () =>
    {
        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IFaceIndexService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        try
        {
            await service.IndexFacesAsync(path);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Face indexing failed for {Path}", path);
        }
    });
    return Results.Ok("started");
});
app.MapGet("/api/debug/p3-status", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IFaceIndexService service) =>
{
    var status = await service.GetIndexStatusAsync(path, ITMartin.Media.Contracts.Contracts.Runtime.Models.FaceIndexIndexType.Faces);
    return Results.Ok(status);
});

// TEMP DEBUG - onboarding a new tenant's unnamed people, removed after
app.MapGet("/api/debug/p3-discover-clusters", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IFaceIndexService service, Microsoft.EntityFrameworkCore.IDbContextFactory<ITMartin.Media.Infrastructure.Persistence.MediaDbContext> dbFactory) =>
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

app.MapPost("/api/debug/p3-name-cluster", async (string path, string name, List<string> clusterMediaFilePaths, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IFaceIndexService service) =>
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

// Same as sf-gps-stats but without the SmartFolders exclusion - for
// inspecting real GPS points already inside a generated Trip/Person folder.
app.MapGet("/api/debug/gps-stats-any", (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IGpsService gps) =>
{
    var files = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
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
            if (sample.Count < 20) sample.Add(new { file = f, lat = coords.Value.lat, lng = coords.Value.lng });
        }
    }

    return Results.Ok(new { total = files.Count, withGps, sample });
});

app.MapGet("/api/debug/sf-people", async (ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IFaceIndexService service) =>
    Results.Ok(await service.GetPeopleAsync()));

app.MapPost("/api/debug/sf-add-person", async (string path, string name, string referencePhotoPath, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IFaceIndexService service) =>
{
    var bytes = await File.ReadAllBytesAsync(referencePhotoPath);
    var personId = await service.AddPersonAsync(name, [new(Path.GetFileName(referencePhotoPath), bytes)], path);
    return Results.Ok(new { personId });
});

app.MapPost("/api/debug/sf-person", async (string path, Guid personId, double? threshold, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ISmartFoldersService service) =>
    Results.Ok(await service.GeneratePersonFolderAsync(path, personId, threshold ?? 0.45)));

app.MapGet("/api/debug/p3-find-matches-count", async (Guid personId, double threshold, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IFaceIndexService service) =>
{
    var matches = await service.FindMatchesAsync(personId, threshold);
    return Results.Ok(new { count = matches.Count });
});

app.MapPost("/api/debug/p3-classify-unhandled", async (string path, int? maxFiles, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IFaceIndexService service) =>
    Results.Ok(await service.ClassifyUnhandledFilesAsync(path, maxFiles ?? 5000)));

app.MapPost("/api/debug/sf-delete-person", async (Guid personId, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IFaceIndexService service) =>
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

app.MapPost("/api/debug/p3-estimate-undated", async (string path, int? maxDatedReferenceFiles, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IFaceIndexService service) =>
    Results.Ok(await service.EstimateUndatedDatesAsync(path, maxDatedReferenceFiles: maxDatedReferenceFiles)));

// see FaceIndexService.DateLivePhotosByFaceMatchAsync.
app.MapPost("/api/debug/p3-date-livephotos", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IFaceIndexService service) =>
    Results.Ok(await service.DateLivePhotosByFaceMatchAsync(path)));

// see FaceIndexService.DateVideosByFaceMatchAsync.
app.MapPost("/api/debug/p3-date-videos", async (string path, int? maxFiles, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IFaceIndexService service) =>
    Results.Ok(await service.DateVideosByFaceMatchAsync(path, maxFiles ?? 1000)));

app.MapPost("/api/debug/p3-date-videos-gps", async (string path, double? homeAwayKm, double? gpsToleranceMeters, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IFaceIndexService service) =>
    Results.Ok(await service.DateVideosByGpsAwayFromHomeAsync(path, homeAwayKm ?? 100, gpsToleranceMeters ?? 2000)));

// Groups a folder's files by "how many faces were detected" using the
// already-computed MediaFaces index (free, no new work) - lets a folder
// full of undated photos be split into "has people" vs "no people
// detected" (landscapes/houses/objects/pets) without any AI vision calls.
app.MapGet("/api/debug/face-count-summary", async (string folderPrefix, Microsoft.EntityFrameworkCore.IDbContextFactory<ITMartin.Media.Infrastructure.Persistence.MediaDbContext> dbFactory) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var rows = await db.MediaFaces
        .Where(x => x.MediaFilePath.StartsWith(folderPrefix))
        .Select(x => new { x.MediaFilePath, x.EmbeddingJson })
        .ToListAsync();

    var byFile = rows
        .GroupBy(x => x.MediaFilePath)
        .Select(g => new
        {
            Path = g.Key,
            FaceCount = g.Count(x => x.EmbeddingJson != "[]"),
        })
        .ToList();

    return Results.Ok(new
    {
        TotalFiles = byFile.Count,
        NoFaceDetected = byFile.Count(x => x.FaceCount == 0),
        OnePerson = byFile.Count(x => x.FaceCount == 1),
        TwoOrMorePeople = byFile.Count(x => x.FaceCount >= 2),
        Files = byFile,
    });
});

// Diagnostic for schema/data-connection drift on a long-lived .media.db -
// e.g. this once revealed the server had been connecting to a stale,
// unrelated database (wrong MediaSettings:LibraryRoot) instead of the
// intended library, which looked like a hang/performance bug but wasn't.
app.MapGet("/api/debug/db-check-indexes", async (Microsoft.EntityFrameworkCore.IDbContextFactory<ITMartin.Media.Infrastructure.Persistence.MediaDbContext> dbFactory) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var conn = db.Database.GetDbConnection();
    await conn.OpenAsync();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT name, sql FROM sqlite_master WHERE type='index' AND tbl_name='MediaFaces';";
    var results = new List<object>();
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
        results.Add(new { Name = reader.GetString(0), Sql = reader.IsDBNull(1) ? null : reader.GetString(1) });

    using var countCmd = conn.CreateCommand();
    countCmd.CommandText = "SELECT COUNT(*) FROM MediaFaces;";
    var count = (long)(await countCmd.ExecuteScalarAsync())!;

    using var pragmaCmd = conn.CreateCommand();
    pragmaCmd.CommandText = "PRAGMA journal_mode;";
    var journalMode = (string)(await pragmaCmd.ExecuteScalarAsync())!;

    return Results.Ok(new { RowCount = count, JournalMode = journalMode, Indexes = results });
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

app.MapPost("/api/debug/reclassify-screenshots", async (string path, int? maxFiles, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ILibraryPolishService service) =>
    Results.Ok(await service.ReclassifyScreenshotsAsync(path, maxFiles ?? 500)));

// Reverse of reclassify-screenshots: finds real screenshots sitting
// misfiled in an Images/Billeder-side folder and moves them into a
// screenshots folder. Real (Haiku-cheap but real) per-file Claude cost -
// maxFiles is a REQUIRED hard cap, same convention as check-orientation-ai.
app.MapPost("/api/debug/find-screenshots-in-images", async (string sourcePath, string destPath, int maxFiles, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ILibraryPolishService service) =>
{
    if (maxFiles <= 0 || maxFiles > 5000)
        return Results.BadRequest("maxFiles must be between 1 and 5000.");
    return Results.Ok(await service.FindScreenshotsInImagesAsync(sourcePath, destPath, maxFiles));
});

// Free-only rotation fix - never touches the paid Claude fallback FixOrientationAsync
// has. Auto-fixes what the local face-detection tier is confident about, reports
// the rest for manual review instead of guessing or skipping silently.
app.MapPost("/api/debug/fix-orientation-free", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ILibraryPolishService service) =>
    Results.Ok(await service.FixOrientationFreeOnlyAsync(path)));

// Same free face-detection check as fix-orientation-free, but report-only -
// never writes anything, for reviewing what's rotated before committing to a fix.
app.MapPost("/api/debug/detect-rotated-images", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ILibraryPolishService service) =>
    Results.Ok(await service.DetectRotatedImagesAsync(path)));

// Free, deterministic rotation fix driven by the file's own EXIF Orientation
// tag - no face-detection guessing. Fixes the case where a viewer (e.g.
// Windows Photos) only flipped the EXIF tag instead of re-encoding pixels,
// so the file looks correct in EXIF-aware viewers but wrong everywhere else.
app.MapPost("/api/debug/bake-exif-orientation", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ILibraryPolishService service) =>
    Results.Ok(await service.BakeExifOrientationAsync(path)));

// One-off census: which files in a library came from a camera known to
// write an unreliable EXIF Orientation tag (see
// IImageConverterService.IsFromOrientationUnreliableCamera). Metadata-only
// read per file (no decode), so safe to run against a whole library.
// One-off cleanup for HEIC/HEIF files that survived a QuickSort run from
// before 2026-08-25's Magick.NET fix (see ImageConverterService) - converts
// in place and swaps the delivered file's extension to .jpg, since the
// delivered library should never contain HEIC (QuickSort always converts).
app.MapPost("/api/debug/convert-heic-inplace", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Workflows.IImageConverterService converter) =>
{
    if (!File.Exists(path))
        return Results.NotFound();

    var ext = Path.GetExtension(path);
    if (!ext.Equals(".heic", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".heif", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest("Not a HEIC/HEIF file.");

    var convertedTempPath = await converter.ConvertToJpgAsync(path);
    if (convertedTempPath is null || string.Equals(convertedTempPath, path, StringComparison.OrdinalIgnoreCase))
        return Results.Ok(new { Success = false, Reason = "Conversion failed or fell back to original", Path = path });

    var finalJpgPath = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + ".jpg");
    if (File.Exists(finalJpgPath))
        return Results.Ok(new { Success = false, Reason = "Target .jpg already exists", Path = path });

    File.Copy(convertedTempPath, finalJpgPath, overwrite: false);
    File.Delete(path);

    return Results.Ok(new { Success = true, NewPath = finalJpgPath });
});

app.MapGet("/api/debug/orientation-unreliable-camera-files", (string path, ITMartin.Media.Contracts.Contracts.Runtime.Workflows.IImageConverterService converter) =>
{
    if (!Directory.Exists(path))
        return Results.NotFound();

    var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg" };
    var matches = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
        .Where(f => exts.Contains(Path.GetExtension(f)) && !f.Contains("\\thumbnails\\", StringComparison.OrdinalIgnoreCase))
        .Where(converter.IsFromOrientationUnreliableCamera)
        .Select(f => Path.GetRelativePath(path, f))
        .OrderBy(f => f)
        .ToList();

    return Results.Ok(new { Count = matches.Count, Files = matches });
});

// AI-vision alternative to the free face-detection check - real Claude cost
// (Haiku, batched 20/call), so maxImages is a REQUIRED hard cap, not just a
// suggestion - this must never be callable without an explicit ceiling on
// how many photos (and therefore how many dollars) one call can trigger.
// Report-only, never writes anything.
app.MapPost("/api/debug/check-orientation-ai", async (string path, int maxImages, ITMartin.Ai.Interfaces.IPhotoOrientationCheckService service) =>
{
    if (maxImages <= 0 || maxImages > 2000)
        return Results.BadRequest("maxImages must be between 1 and 2000.");
    if (!Directory.Exists(path))
        return Results.NotFound();

    var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".heic", ".webp" };
    var images = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
        .Where(f => extensions.Contains(Path.GetExtension(f)))
        .Take(maxImages)
        .Select(f => (FullPath: f, RelativePath: Path.GetRelativePath(path, f)))
        .ToList();

    var allResults = new List<ITMartin.Ai.Models.PhotoOrientationResult>();
    var apiCalls = 0;
    foreach (var batch in images.Chunk(ITMartin.Ai.Services.ClaudePhotoOrientationCheckService.BatchSize))
    {
        apiCalls++;
        allResults.AddRange(await service.CheckBatchAsync(batch));
    }

    return Results.Ok(new
    {
        photosChecked = images.Count,
        apiCalls,
        needsRotation = allResults.Where(r => r.NeedsRotation).ToList(),
        allResults,
    });
});

// Runs against whatever's actually on disk right now (not just one QuickSort
// run's own file set) - catches duplicates introduced by merging separate
// folders/runs together after the fact. Free, local, never auto-deletes.
app.MapPost("/api/debug/find-duplicates", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ILibraryPolishService service) =>
    Results.Ok(await service.FindDuplicatesInLibraryAsync(path)));

app.MapPost("/api/debug/p4-verify-delivery-structure", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ILibraryVerifyService service) =>
    Results.Ok(await service.VerifyDeliveryStructureAsync(path)));

// Runs every applicable step-flag (CategoryIsSet, SubCategoryIsSet, DateIsSet,
// RotationIsCorrect free-tier, NotDuplicate, IsNormalized, QualityChecked,
// FileIsReadable) against an already-sorted library - see FileStatusWorkflowStep
// for the fresh-import equivalent. Only files not already IsDone in
// filestatus.json get looked at, so re-running the same library only ever
// costs what's newly unresolved. maxAiCalls is a REQUIRED hard cap (real
// Claude cost for whatever's still ambiguous after the free tiers).
app.MapPost("/api/debug/run-all-steps", async (string path, int maxAiCalls, int? maxRotationParallelism, bool? includeSlowSteps, int? maxRotationChecksPerRun, int? maxFilesScannedPerRun, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ILibraryPolishService service) =>
{
    if (maxAiCalls < 0 || maxAiCalls > 5000)
        return Results.BadRequest("maxAiCalls must be between 0 and 5000.");
    if (maxRotationParallelism is < 1)
        return Results.BadRequest("maxRotationParallelism must be at least 1.");
    if (maxRotationChecksPerRun is < 0)
        return Results.BadRequest("maxRotationChecksPerRun must be at least 0.");
    if (maxFilesScannedPerRun is < 0)
        return Results.BadRequest("maxFilesScannedPerRun must be at least 0.");
    return Results.Ok(await service.RunAllStepsAsync(path, maxAiCalls, maxRotationParallelism, includeSlowSteps ?? true, maxRotationChecksPerRun, maxFilesScannedPerRun));
});

// Automates re-triggering run-all-steps round after round until the
// residual stops shrinking (or every file is done) - see
// ILibraryPolishService.RunUntilConvergedAsync. maxAiCalls is the PER-ROUND
// cap, same required-hard-cap convention as run-all-steps.
app.MapPost("/api/debug/run-until-converged", async (string path, int maxAiCalls, int? maxRotationParallelism, int? maxIterations, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ILibraryPolishService service) =>
{
    if (maxAiCalls < 0 || maxAiCalls > 5000)
        return Results.BadRequest("maxAiCalls must be between 0 and 5000.");
    if (maxRotationParallelism is < 1)
        return Results.BadRequest("maxRotationParallelism must be at least 1.");
    if (maxIterations is < 1)
        return Results.BadRequest("maxIterations must be at least 1.");
    return Results.Ok(await service.RunUntilConvergedAsync(path, maxAiCalls, maxRotationParallelism, maxIterations ?? 10));
});

// Read-only view of the isDone registry - "viewable state" for a library:
// counts by category/flag, and a sample of what still needs manual review.
app.MapGet("/api/debug/file-status-report", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IFileStatusRegistryService registry) =>
{
    var loaded = await registry.LoadAsync(path);
    return Results.Ok(registry.BuildReport(loaded));
});

// One-time cleanup for "BurstN" folders found in already-sorted libraries -
// see LibraryPolishService.FlattenBurstFoldersAsync.
app.MapPost("/api/debug/flatten-bursts", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ILibraryPolishService service) =>
    Results.Ok(await service.FlattenBurstFoldersAsync(path)));

// see LibraryPolishService.ReclassifyAlbumArtAsync.
app.MapPost("/api/debug/reclassify-albumart", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ILibraryPolishService service) =>
    Results.Ok(await service.ReclassifyAlbumArtAsync(path)));

// see LibraryPolishService.FindNonPhotoClustersAsync. Report-only.
app.MapPost("/api/debug/find-nonphoto-clusters", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ILibraryPolishService service) =>
    Results.Ok(await service.FindNonPhotoClustersAsync(path)));

// see LibraryPolishService.ReclassifyWebWatermarksAsync.
app.MapPost("/api/debug/reclassify-web-watermarks", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ILibraryPolishService service) =>
    Results.Ok(await service.ReclassifyWebWatermarksAsync(path)));

// TEMP DEBUG - one-off merge of Musik/_Genfundet fra Ikke_identificeret
// (flat pile of recovered audio files, no artist/album structure) back into
// the real Musik/{Artist}/{Album}/ tree, using ID3/TagLib metadata since
// most filenames here don't include the artist. Files with no readable
// Artist tag are left in place - reported, not guessed at.
app.MapPost("/api/debug/musik-merge-genfundet", (string musikPath, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IAudioMetadataService audioMeta) =>
{
    var genfundetDir = System.IO.Path.Combine(musikPath, "_Genfundet fra Ikke_identificeret");
    if (!System.IO.Directory.Exists(genfundetDir)) return Results.Ok(new { checked_ = 0, moved = 0, noArtist = new List<string>() });

    var files = System.IO.Directory.EnumerateFiles(genfundetDir, "*", System.IO.SearchOption.TopDirectoryOnly).ToList();
    var moved = 0;
    var noArtist = new List<string>();

    foreach (var file in files)
    {
        var artist = audioMeta.GetArtist(file);
        if (string.IsNullOrWhiteSpace(artist))
        {
            noArtist.Add(System.IO.Path.GetFileName(file));
            continue;
        }

        var album = audioMeta.GetAlbum(file);
        var safeArtist = string.Join("_", artist.Split(System.IO.Path.GetInvalidFileNameChars()));
        var safeAlbum = string.IsNullOrWhiteSpace(album)
            ? "Diverse"
            : string.Join("_", album!.Split(System.IO.Path.GetInvalidFileNameChars()));

        var destDir = System.IO.Path.Combine(musikPath, safeArtist, safeAlbum);
        System.IO.Directory.CreateDirectory(destDir);

        var destPath = System.IO.Path.Combine(destDir, System.IO.Path.GetFileName(file));
        var i = 1;
        while (System.IO.File.Exists(destPath))
        {
            var n = System.IO.Path.GetFileNameWithoutExtension(file);
            var e = System.IO.Path.GetExtension(file);
            destPath = System.IO.Path.Combine(destDir, $"{n} ({i}){e}");
            i++;
        }

        System.IO.File.Move(file, destPath);
        moved++;
    }

    return Results.Ok(new { checked_ = files.Count, moved, noArtist });
});

// "Just before delivery" package (2026-08-20) - runs every free/local check
// together in one call: file integrity, structure/extensions, rotation
// (free tier only), duplicates (exact + near). Nothing here costs anything
// or auto-deletes/auto-moves beyond what each individual check already does
// on its own (orientation fixes confident rotations; everything else only
// reports). Meant to run right before a library ships to a customer's HD/USB.
app.MapPost("/api/debug/pre-delivery-check", async (
    string path,
    ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ILibraryVerifyService package4,
    ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ILibraryPolishService polish) =>
{
    var integrity   = await package4.VerifyLibraryAsync(path);
    var structure   = await package4.VerifyStructureAsync(path);
    var delivery    = await package4.VerifyDeliveryStructureAsync(path);
    var orientation = await polish.FixOrientationFreeOnlyAsync(path);
    var duplicates  = await polish.FindDuplicatesInLibraryAsync(path);

    return Results.Ok(new
    {
        integrity,
        structure,
        delivery,
        orientation,
        duplicates,
        checkedAtUtc = DateTime.UtcNow,
    });
});

// Package4 - library health check. Actually opens/decodes every file and
// reports which ones fail, rather than trusting extension/codec metadata.
// Free, local-only, read-only (never modifies anything).
app.MapPost("/api/debug/p4-verify", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ILibraryVerifyService service) =>
    Results.Ok(await service.VerifyLibraryAsync(path)));

// Structure/path-portability check - metadata-only (no file content read), so
// this is safe to point straight at a NAS path or an external HD in place,
// without copying the library back locally first. Catches the collections.json
// backslash/absolute-path bug class documented in feedback_walk_through_ux.
app.MapPost("/api/debug/p4-verify-structure", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ILibraryVerifyService service) =>
    Results.Ok(await service.VerifyStructureAsync(path)));

// Fixes what p4-verify-structure finds in collections.json in place - never
// re-sorts, never touches real library content.
app.MapPost("/api/debug/p4-repair-collections", async (string path, ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.ILibraryVerifyService service) =>
    Results.Ok(await service.RepairCollectionsPathsAsync(path)));

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
