using ITMartin.Media.Application.Interfaces;
using ITMartin.Media.Application.Pipelines;
using ITMartin.Media.Application.Processors;
using ITMartin.Media.Application.Services;
using ITMartin.Media.Domain.Interfaces;
using ITMartin.Media.Domain.Models;
using ITMartin.Media.Infrastructure;
using ITMartin.Media.Infrastructure.Services;
using ITMartin.Media.Interfaces;
using ITMartin.OCR.Interfaces;
using ITMartin.OCR.Services;
using ITMartinFileSorter.Application.Services;
using ITMartinFileSorter.Server;
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

builder.Services
    .AddServerSideBlazor()
    .AddCircuitOptions(options =>
    {
        options.DetailedErrors = true;
    });

// =========================
// MEDIA PLATFORM
// =========================

builder.Services.AddMediaInfrastructure(
    builder.Configuration);

// =========================
// PIPELINES
// =========================

builder.Services.AddScoped<
    Package1ScanPipeline>();

builder.Services.AddScoped<
    Package1ExportPipeline>();

builder.Services.AddScoped<
    Package1Pipeline>();

// =========================
// DUPLICATES
// =========================

builder.Services.AddScoped<
    IDuplicateService,
    DuplicateService>();

// =========================
// EXPORT
// =========================

builder.Services.AddScoped<
    ILibraryExportService,
    LibraryExportService>();

// =========================
// UI / WORKFLOW
// =========================

builder.Services.AddScoped<
    ProgressService>();

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
// AI
// KEEP FOR PACKAGE 2
// =========================

builder.Services.AddScoped<
    IMediaVisionService,
    MediaVisionService>();

builder.Services.AddScoped<
    IAiCollectionService,
    AiCollectionService>();

builder.Services.AddScoped<
    IAiCacheService,
    SqliteAiCacheService>();

builder.Services.AddScoped<
    IMediaOcrService,
    MediaOcrService>();

// =========================
// OCR
// KEEP FOR PACKAGE 2
// =========================

builder.Services.AddSingleton<
    IOcrService,
    OcrService>();


// =========================
// NAMING
// =========================

builder.Services.AddScoped<
    IMediaNamingService,
    MediaNamingService>();

// =========================
// CONTROLLERS
// =========================

builder.Services.AddControllers();

// =========================
// SQLITE
// =========================

var dataFolder =
    @"C:\FileSorterData";

Directory.CreateDirectory(
    dataFolder);

var dbPath =
    Path.Combine(
        dataFolder,
        "media.db");

builder.Services.AddDbContextFactory<
    MediaDbContext>(options =>
{
    options.UseSqlite(
        $"Data Source={dbPath}");
});

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

// wwwroot
app.UseStaticFiles();

// =========================
// LIBRARY FILES
// =========================

var libraryPath =
    builder.Configuration[
        "MediaSettings:LibraryRoot"];

var provider =
    new FileExtensionContentTypeProvider();

// =========================
// VIDEO
// =========================

provider.Mappings[".mp4"] =
    "video/mp4";

provider.Mappings[".MP4"] =
    "video/mp4";

provider.Mappings[".mov"] =
    "video/quicktime";

provider.Mappings[".MOV"] =
    "video/quicktime";

provider.Mappings[".mkv"] =
    "video/x-matroska";

provider.Mappings[".MKV"] =
    "video/x-matroska";

// =========================
// IMAGES
// =========================

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

provider.Mappings[".JPG"] =
    "image/jpeg";

provider.Mappings[".JPEG"] =
    "image/jpeg";

provider.Mappings[".PNG"] =
    "image/png";

provider.Mappings[".WEBP"] =
    "image/webp";

provider.Mappings[".GIF"] =
    "image/gif";

// =========================
// MODERN / IPHONE
// =========================

provider.Mappings[".heic"] =
    "image/heic";

provider.Mappings[".HEIC"] =
    "image/heic";

provider.Mappings[".avif"] =
    "image/avif";

provider.Mappings[".AVIF"] =
    "image/avif";

// =========================
// STATIC LIBRARY FILES
// =========================

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
                provider,

            OnPrepareResponse = ctx =>
            {
                const int duration =
                    60 * 60 * 24 * 30;

                ctx.Context
                    .Response
                    .Headers
                    .Append(
                        "Cache-Control",
                        $"public,max-age={duration}");
            }
        });
}

// =========================
// PIPELINE
// =========================

app.UseAntiforgery();

app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// =========================
// TEST ENDPOINT
// KEEP FOR NOW
// =========================

app.MapGet(
    "/api/scan-preview",
    (IFileScanner scanner) =>
    {
        var results =
            scanner
                .EnumerateFiles(
                    @"C:\FileSorterTests\Test1_Source")
                .Take(50)
                .Select(path =>
                    new AiScanResultViewModel
                    {
                        FullPath = path,

                        FileName =
                            Path.GetFileName(path)
                    })
                .ToList();

        return Results.Ok(results);
    });
// =========================
// THUMBNAILS
// KEEP FOR PACKAGE 2
// =========================

app.MapGet(
    "/thumbnail",
    (string path) =>
{
    if (!System.IO.File.Exists(path))
    {
        return Results.NotFound();
    }

    var ext =
        Path.GetExtension(path);

    var contentType =
        ext.ToLower() switch
        {
            ".jpg" =>
                "image/jpeg",

            ".jpeg" =>
                "image/jpeg",

            ".png" =>
                "image/png",

            ".gif" =>
                "image/gif",

            ".webp" =>
                "image/webp",

            _ =>
                "application/octet-stream"
        };

    return Results.File(
        path,
        contentType);
});

app.Run();