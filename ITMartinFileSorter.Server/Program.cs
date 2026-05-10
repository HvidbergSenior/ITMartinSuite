using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using ITMartinFileSorter.Application.Services;
using ITMartinFileSorter.Application.Helpers;
using ITMartinFileSorter.Domain.Interfaces;
using ITMartinFileSorter.Infrastructure.FileSystem;
using ITMartinFileSorter.Infrastructure.Services;
using ITMartinFileSorter.Server;

var builder = WebApplication.CreateBuilder(args);

// =========================
// BLAZOR
// =========================
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddServerSideBlazor()
    .AddCircuitOptions(options =>
    {
        options.DetailedErrors = true;
    });

// =========================
// CORE SERVICES
// =========================
builder.Services.AddScoped<IFileScanner, FileScanner>();
builder.Services.AddScoped<IFileSystem, FileSystemService>();

builder.Services.AddScoped<IHashService, Sha256HashService>();
builder.Services.AddScoped<IMediaDateService, MediaDateService>();
builder.Services.AddScoped<IMediaClassificationService, MediaClassificationService>();
builder.Services.AddScoped<IExifService, ExifService>();
builder.Services.AddScoped<IGpsService, GpsService>();

// =========================
// CATEGORIZERS
// =========================
builder.Services.AddScoped<IMediaSubCategorizer, ImageCategorizer>();
builder.Services.AddScoped<IMediaSubCategorizer, VideoCategorizer>();
builder.Services.AddScoped<IMediaSubCategorizer, AudioCategorizer>();
builder.Services.AddScoped<IMediaSubCategorizer, DocumentCategorizer>();

builder.Services.AddScoped<MediaCategorizer>();

// =========================
// APP SERVICES
// =========================
builder.Services.AddScoped<DuplicateService>();
builder.Services.AddScoped<ProgressService>();
builder.Services.AddScoped<HomeLocationService>();
builder.Services.AddScoped<LibraryExportService>();
builder.Services.AddScoped<FolderPathInfoService>();

// =========================
// MEDIA PROCESSING
// =========================
builder.Services.AddScoped<FastUniversalVideoConverterService>();
builder.Services.AddScoped<VideoBatchService>();

builder.Services.AddScoped<UniversalImageConverterService>();
builder.Services.AddScoped<ImageBatchService>();

builder.Services.AddScoped<ThumbnailService>();
builder.Services.AddScoped<SubtitleService>();

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
    app.UseExceptionHandler("/Error");
}

// =========================
// STATIC FILES
// =========================

// wwwroot
app.UseStaticFiles();

// Library path
var libraryPath =
    LibraryPathHelper.GetLibraryPath(
        builder.Configuration);

// MIME types
var provider =
    new FileExtensionContentTypeProvider();

// =========================
// VIDEO
// =========================
provider.Mappings[".mp4"] = "video/mp4";
provider.Mappings[".MP4"] = "video/mp4";

provider.Mappings[".mov"] = "video/quicktime";
provider.Mappings[".MOV"] = "video/quicktime";

provider.Mappings[".mkv"] = "video/x-matroska";
provider.Mappings[".MKV"] = "video/x-matroska";

// =========================
// IMAGES
// =========================
provider.Mappings[".jpg"] = "image/jpeg";
provider.Mappings[".jpeg"] = "image/jpeg";
provider.Mappings[".png"] = "image/png";
provider.Mappings[".webp"] = "image/webp";
provider.Mappings[".gif"] = "image/gif";

provider.Mappings[".JPG"] = "image/jpeg";
provider.Mappings[".JPEG"] = "image/jpeg";
provider.Mappings[".PNG"] = "image/png";
provider.Mappings[".WEBP"] = "image/webp";
provider.Mappings[".GIF"] = "image/gif";

// =========================
// IPHONE / MODERN
// =========================
provider.Mappings[".heic"] = "image/heic";
provider.Mappings[".HEIC"] = "image/heic";

provider.Mappings[".avif"] = "image/avif";
provider.Mappings[".AVIF"] = "image/avif";

// =========================
// LIBRARY STATIC FILES
// =========================
if (Directory.Exists(libraryPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider =
            new PhysicalFileProvider(libraryPath),

        RequestPath =
            "/libraryfiles",

        ContentTypeProvider =
            provider,

        OnPrepareResponse = ctx =>
        {
            const int duration =
                60 * 60 * 24 * 30;

            ctx.Context.Response.Headers.Append(
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

app.Run();