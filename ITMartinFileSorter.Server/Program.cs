using ITMartin.Media.Infrastructure;
using ITMartin.Media.Interfaces;
using ITMartinFileSorter.Application.Services;
using ITMartinFileSorter.Server;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using ITMartin.OCR.Interfaces;
using ITMartin.OCR.Services;
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
// MEDIA PLATFORM
// =========================
builder.Services.AddMediaInfrastructure(
    builder.Configuration);
builder.Services.AddSingleton<
    IOcrService,
    OcrService>();
// =========================
// APP SERVICES
// =========================
builder.Services.AddScoped<DuplicateService>();
builder.Services.AddScoped<ProgressService>();
builder.Services.AddScoped<HomeLocationService>();
builder.Services.AddScoped<LibraryExportService>();
builder.Services.AddScoped<FolderPathInfoService>();

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

// =========================
// LIBRARY FILES
// =========================

var libraryPath =
    builder.Configuration["MediaSettings:LibraryRoot"];

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
// MODERN / IPHONE
// =========================
provider.Mappings[".heic"] = "image/heic";
provider.Mappings[".HEIC"] = "image/heic";

provider.Mappings[".avif"] = "image/avif";
provider.Mappings[".AVIF"] = "image/avif";

// =========================
// STATIC LIBRARY FILES
// =========================
if (!string.IsNullOrWhiteSpace(libraryPath) &&
    Directory.Exists(libraryPath))
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