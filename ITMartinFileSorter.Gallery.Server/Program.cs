using ITMartin.Ai;
using ITMartin.Media.Infrastructure.DependencyInjection;
using ITMartinFileSorter.Gallery.Server;
using ITMartinFileSorter.Gallery.Server.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services
    .AddServerSideBlazor()
    .AddCircuitOptions(options =>
    {
        options.DetailedErrors = true;
    });

builder.Services.AddFileSorterCore();
builder.Services.AddAi();
builder.Services.AddSingleton<ToastService>();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.None);

builder.Services.AddScoped(sp =>
{
    var navigation = sp.GetRequiredService<NavigationManager>();
    return new HttpClient { BaseAddress = new Uri(navigation.BaseUri) };
});

builder.Services.AddControllers();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();

var libraryPath = builder.Configuration["MediaSettings:LibraryRoot"];

var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".mp4"]  = "video/mp4";
provider.Mappings[".mov"]  = "video/quicktime";
provider.Mappings[".mkv"]  = "video/x-matroska";
provider.Mappings[".jpg"]  = "image/jpeg";
provider.Mappings[".jpeg"] = "image/jpeg";
provider.Mappings[".png"]  = "image/png";
provider.Mappings[".webp"] = "image/webp";
provider.Mappings[".gif"]  = "image/gif";
provider.Mappings[".heic"] = "image/heic";
provider.Mappings[".avif"] = "image/avif";

if (!string.IsNullOrWhiteSpace(libraryPath) && Directory.Exists(libraryPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider     = new PhysicalFileProvider(libraryPath),
        RequestPath      = "/libraryfiles",
        ContentTypeProvider = provider
    });
}

app.UseAntiforgery();
app.MapControllers();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
