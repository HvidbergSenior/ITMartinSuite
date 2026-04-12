using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using ITMartinFileSorter.Application.Services;
using ITMartinFileSorter.Domain.Interfaces;
using ITMartinFileSorter.Infrastructure.FileSystem;
using ITMartinFileSorter.Infrastructure.Services;
using ITMartinFileSorter.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<IFileScanner, FileScanner>();
builder.Services.AddScoped<IHashService, Sha256HashService>();

builder.Services.AddScoped<MediaCategorizer>();
builder.Services.AddScoped<AudioCategorizer>();
builder.Services.AddScoped<ImageCategorizer>();
builder.Services.AddScoped<VideoCategorizer>();
builder.Services.AddScoped<DocumentCategorizer>();

builder.Services.AddSingleton<DuplicateService>();
builder.Services.AddScoped<TripGroupingService>();

builder.Services.AddScoped<FastUniversalVideoConverterService>();
builder.Services.AddScoped<FastVideoBatchExportService>();

builder.Services.AddScoped<UniversalImageConverterService>();
builder.Services.AddScoped<ImageBatchExportService>();
builder.Services.AddScoped<SubtitleService>();
builder.Services.AddScoped<FolderPathInfoService>();

builder.Services.AddScoped<HomeLocationService>();
builder.Services.AddScoped<JunkDetectionService>();
builder.Services.AddScoped<ProgressService>();
builder.Services.AddScoped<ThumbnailService>();
builder.Services.AddScoped<ArchivePathBuilder>();
builder.Services.AddScoped<IMediaDateService, MediaDateService>();
builder.Services.AddScoped<AlbumStyleNameBuilder>();
builder.Services.AddScoped<IGpsService, GpsService>();
builder.Services.AddScoped<FileRenameService>();

builder.Services.AddControllers();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();

// Serve exported library files (videos/images) outside wwwroot
var libraryRoot = Path.Combine(
    builder.Configuration["MediaSettings:LibraryRoot"]!,
    "Library");

if (Directory.Exists(libraryRoot))
{
    var provider = new FileExtensionContentTypeProvider();

    provider.Mappings[".mp4"] = "video/mp4";
    provider.Mappings[".mov"] = "video/mp4";
    provider.Mappings[".jpg"] = "image/jpeg";
    provider.Mappings[".jpeg"] = "image/jpeg";
    provider.Mappings[".png"] = "image/png";
    provider.Mappings[".webp"] = "image/webp";

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(libraryRoot),
        RequestPath = "/libraryfiles",
        ContentTypeProvider = provider
    });
}

app.UseAntiforgery();

app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();