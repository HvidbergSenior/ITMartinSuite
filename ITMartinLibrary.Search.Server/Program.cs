using ITMartin.Ai;
using ITMartinLibrary.Application;
using ITMartinLibrary.Infrastructure;
using ITMartinLibrary.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLibraryApplication();
builder.Services.AddLibraryInfrastructure(builder.Configuration);
builder.Services.AddAi();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
    await db.Database.EnsureCreatedAsync();
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<ITMartinLibrary.Search.Server.App>()
    .AddInteractiveServerRenderMode();

app.Run();
