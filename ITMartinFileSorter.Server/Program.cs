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
    .AddCircuitOptions(options => { options.DetailedErrors = true; });

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
builder.Services.AddScoped<IVideoBatchService, VideoBatchService>();
builder.Services.AddScoped<IImageBatchService, ImageBatchService>();
// =========================
// CATEGORIZERS (INTERFACE-BASED)
// =========================
builder.Services.AddScoped<IMediaSubCategorizer, ImageCategorizer>();
builder.Services.AddScoped<IMediaSubCategorizer, VideoCategorizer>();
builder.Services.AddScoped<IMediaSubCategorizer, AudioCategorizer>();
builder.Services.AddScoped<IMediaSubCategorizer, DocumentCategorizer>();

builder.Services.AddScoped<MediaCategorizer>();

// =========================
// APP SERVICES
// =========================
builder.Services.AddSingleton<DuplicateService>();

builder.Services.AddScoped<ProgressService>();
builder.Services.AddScoped<HomeLocationService>();
builder.Services.AddScoped<LibraryPathService>();
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

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

// =========================
// STATIC FILES
// =========================
app.UseStaticFiles(); // wwwroot

var libraryPath = LibraryPathHelper.GetLibraryPath(builder.Configuration);

Console.WriteLine($"[STATIC] Serving from: {libraryPath}");

var provider = new FileExtensionContentTypeProvider();

// video
provider.Mappings[".mp4"] = "video/mp4";
provider.Mappings[".mov"] = "video/mp4";
provider.Mappings[".mkv"] = "video/mp4";

// images
provider.Mappings[".jpg"] = "image/jpeg";
provider.Mappings[".jpeg"] = "image/jpeg";
provider.Mappings[".png"] = "image/png";
provider.Mappings[".webp"] = "image/webp";
provider.Mappings[".gif"] = "image/gif";

// register external library folder
if (Directory.Exists(libraryPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(libraryPath),
        RequestPath = "/libraryfiles",
        ContentTypeProvider = provider
    });
}
else
{
    Console.WriteLine("[ERROR] Library path does NOT exist!");
}

// =========================
// PIPELINE
// =========================
app.UseAntiforgery();

app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();