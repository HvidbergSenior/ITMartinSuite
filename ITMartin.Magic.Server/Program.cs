using ITMartin.Ai;
using ITMartin.Ai.Interfaces;
using ITMartin.Ai.Services;
using ITMartin.Magic.Application;
using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Infrastructure;
using ITMartin.Magic.Infrastructure.Persistence;
using ITMartin.Magic.Infrastructure.Services;
using ITMartin.Magic.Server;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Infrastructure.DependencyInjection;
using ITMartin.Media.Infrastructure.Services;
using ITMartin.OCR;
using ITMartin.OCR.Interfaces;
using ITMartin.OCR.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder =
    WebApplication.CreateBuilder(args);
builder.Logging.AddFilter(
    "Microsoft.EntityFrameworkCore.Database.Command",
    LogLevel.Warning);

builder.Logging.AddFilter(
    "Microsoft.AspNetCore",
    LogLevel.Warning);
builder.Services.AddMediaCore(builder.Configuration);
builder.Services.AddMediaRuntime(builder.Configuration);
builder.Services.AddMagicApplication(builder.Configuration);
builder.Services.AddMagicInfrastructure(builder.Configuration);
builder.Services.AddAi();
builder.Services.AddOcr();
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
// OPENCV
// =========================

builder.Services.AddScoped<
    IBlurDetectionService,
    OpenCvBlurDetectionService>();

builder.Services.AddScoped<
    IOcrRegionExtractor,
    OpenCvMagicCardOcrRegionExtractor>();


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
using var scope =
    app.Services.CreateScope();

var db =
    scope.ServiceProvider
        .GetRequiredService<MagicDbContext>();

await db.Database.MigrateAsync();

var importer =
    scope.ServiceProvider
        .GetRequiredService<IMagicSetImportService>();

await importer.ImportAsync(
    CancellationToken.None);

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