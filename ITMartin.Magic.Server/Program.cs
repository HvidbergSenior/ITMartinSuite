using ITMartin.Ai.Interfaces;
using ITMartin.Ai.Services;
using ITMartin.Magic.Application;
using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Infrastructure.Services;
using ITMartin.Magic.Server;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Infrastructure.Services;
using ITMartin.OCR.Interfaces;
using ITMartin.OCR.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.FileProviders;

var builder =
    WebApplication.CreateBuilder(args);

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
// APPLICATION
// =========================

builder.Services
    .AddMagicApplication();

// =========================
// OCR
// =========================

builder.Services.AddScoped<
    IOcrService,
    OcrService>();

// =========================
// AI
// =========================

builder.Services.AddScoped<
    IImageAnalysisService,
    OpenAiImageAnalysisService>();

builder.Services.AddScoped<
    IMagicCardRecognitionService,
    OpenAiMagicCardRecognitionService>();

// =========================
// OPENCV
// =========================

builder.Services.AddScoped<
    ICardLayoutDetectionService,
    CardLayoutDetectionService>();

builder.Services.AddScoped<
    ICardCornerDetectionService,
    OpenCvCardCornerDetectionService>();

builder.Services.AddScoped<
    IPerspectiveCorrectionService,
    OpenCvPerspectiveCorrectionService>();

builder.Services.AddScoped<
    IBlurDetectionService,
    OpenCvBlurDetectionService>();

builder.Services.AddScoped<
    IOcrRegionExtractor,
    OpenCvMagicCardOcrRegionExtractor>();

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
        "data/debug",
        "data/ocr"
    };

foreach (var folder in dataFolders)
{
    Directory.CreateDirectory(folder);
}

// =========================
// BUILD
// =========================

var app =
    builder.Build();

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

        RequestPath =
            "/data"
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