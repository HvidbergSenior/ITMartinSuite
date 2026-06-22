using ITMartinFamily.Infrastructure;
using ITMartinFamily.Server.Components;
using ITMartinFamily.Server.Hubs;
using ITMartinFamily.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSignalR();
builder.Services.AddFamilyInfrastructure(builder.Configuration);
builder.Services.AddSingleton<ToastService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FamilyDbContext>();
    await db.Database.EnsureCreatedAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "data", "tasks"));

app.UseStaticFiles();
app.UseAntiforgery();

app.MapHub<FamilyHub>("/hubs/family");

app.MapGet("/task-image/{id:guid}", async (Guid id, ITMartinFamily.Application.Interfaces.IDailyTaskRepository repo) =>
{
    var task = await repo.GetByIdAsync(id);
    if (task?.ImagePath is null || !File.Exists(task.ImagePath)) return Results.NotFound();
    return Results.File(await File.ReadAllBytesAsync(task.ImagePath), "image/jpeg");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
