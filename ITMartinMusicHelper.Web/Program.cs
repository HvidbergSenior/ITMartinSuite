using ITMartinMusicHelper.Application.Interfaces;
using ITMartinMusicHelper.Application.Services;
using ITMartinMusicHelper.Web;

var builder = WebApplication.CreateBuilder(args);

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