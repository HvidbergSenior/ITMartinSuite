using ITMartinMusicHelper.Application.Interfaces;
using ITMartinMusicHelper.Application.Services;
using ITMartinMusicHelper.Web;
using ITMartinMusicHelper.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<GuitarAiService>();

// 🔥 Correct DI (interface → implementation)
builder.Services.AddScoped<IChordService, ChordService>();
builder.Services.AddScoped<IMelodyService, MelodyService>();
builder.Services.AddScoped<IPickingService, PickingService>();
builder.Services.AddScoped<IPracticeService, PracticeService>();
builder.Services.AddScoped<IStructureService, StructureService>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();