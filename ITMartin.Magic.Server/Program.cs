using ITMartin.Magic.Server;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.FileProviders;
using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Infrastructure.OCR;
using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Infrastructure.Scryfall;
using ITMartin.Media.Domain.Interfaces;
using ITMartin.Media.Domain.Models;
using ITMartin.Media.Infrastructure.Ai;
using ITMartin.OCR.Interfaces;
using ITMartin.OCR.Services;

var builder = WebApplication.CreateBuilder(args);

// =========================
// SERVICES
// =========================

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<HubOptions>(
    options =>
    {
        options.MaximumReceiveMessageSize =
            1024 * 1024 * 20;
    });

builder.Services.AddHttpClient<
    IScryfallService,
    ScryfallService>();
builder.Services.AddScoped<
    IOcrService,
    OcrService>();
builder.Services.AddScoped<
    ICardRecognitionService,
    CardRecognitionService>();
builder.Services.AddScoped<
    IAiAnalysisService,
    OpenAiAnalysisService>();
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
// FRAME STORAGE
// =========================

var framesPath =
    Path.Combine(
        builder.Environment.ContentRootPath,
        "data",
        "frames");

Directory.CreateDirectory(framesPath);

app.UseStaticFiles(
    new StaticFileOptions
    {
        FileProvider =
            new PhysicalFileProvider(framesPath),

        RequestPath = "/frames"
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