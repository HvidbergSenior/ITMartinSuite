using ITMartin.Magic.Server;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// =========================
// SERVICES
// =========================

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers();

builder.Services.AddHttpClient();

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

app.UseHttpsRedirection();

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
// CONTROLLERS
// =========================

app.MapControllers();

// =========================
// BLAZOR
// =========================

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// =========================
// RUN
// =========================

app.Run();