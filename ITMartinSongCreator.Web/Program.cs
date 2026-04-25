using ITMartinSongCreator.Application.Services;
using ITMartinSongCreator.Server.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<IChordService, ChordService>();
builder.Services.AddScoped<IPickingService, PickingService>();
builder.Services.AddScoped<IStructureService, StructureService>();
builder.Services.AddScoped<IMelodyService, MelodyService>();
builder.Services.AddScoped<IPracticeService, PracticeService>();
// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();