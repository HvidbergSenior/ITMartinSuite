using ITMartin.Magic.Server;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.FileProviders;

using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Infrastructure.OCR;
using ITMartin.Magic.Infrastructure.Scryfall;
using ITMartin.Magic.Infrastructure.Services;

using ITMartin.Media.Domain.Interfaces;
using ITMartin.Media.Infrastructure.Ai;
using ITMartin.Media.Infrastructure.Services;
using ITMartin.OCR.Interfaces;
using ITMartin.OCR.Services;

var builder = WebApplication.CreateBuilder(args);

// =========================
// SERVICES
// =========================

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

// =========================
// SIGNALR
// =========================

builder.Services.Configure<HubOptions>(
    options =>
    {
        options.MaximumReceiveMessageSize =
            1024 * 1024 * 20;
    });

// =========================
// HTTP
// =========================

builder.Services.AddHttpClient<
    IScryfallService,
    ScryfallService>();

// =========================
// OCR
// =========================

builder.Services.AddScoped<
    IOcrService,
    OcrService>();

builder.Services.AddScoped<
    ICardRecognitionService,
    CardRecognitionService>();

// =========================
// AI
// =========================

builder.Services.AddScoped<
    IImageAnalysisService,
    OpenAiImageAnalysisService>();

builder.Services.AddScoped<
    IMagicCardAnalysisService,
    OpenAiMagicCardAnalysisService>();
builder.Services.AddScoped<
    IBorderDetectionService,
    BorderDetectionService>();
// =========================
// OPENCV PIPELINE
// =========================

builder.Services.AddScoped<
    ICardBoundaryDetectionService,
    OpenCvCardBoundaryDetectionService>();

builder.Services.AddScoped<
    IPerspectiveCorrectionService,
    OpenCvPerspectiveCorrectionService>();

builder.Services.AddScoped<
    IBlurDetectionService,
    OpenCvBlurDetectionService>();

builder.Services.AddScoped<
    IOcrRegionExtractor,
    OpenCvOcrRegionExtractor>();

// =========================
// URLS
// =========================

builder.WebHost.UseUrls(
    "https://0.0.0.0:5020");

// =========================
// DATA FOLDERS
// =========================

var dataFolders =
    new[]
    {
        "data",
        "data/frames",
        "data/debug",
        "data/normalized",
        "data/ocr"
    };

foreach (var folder in dataFolders)
{
    Directory.CreateDirectory(folder);
}

// =========================
// BUILD
// =========================

var app = builder.Build();

// =========================
// PIPELINE
// =========================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");

    app.UseHsts();
}

// app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseAntiforgery();

// =========================
// STATIC DATA ACCESS
// =========================

var dataPath =
    Path.Combine(
        builder.Environment.ContentRootPath,
        "data");

app.UseStaticFiles(
    new StaticFileOptions
    {
        FileProvider =
            new PhysicalFileProvider(dataPath),

        RequestPath = "/data"
    });

// =========================
// BLAZOR
// =========================

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// =========================
// RUN
// =========================

app.Run();