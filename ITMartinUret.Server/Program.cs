using ITMartinUret.Server.Components;
using ITMartinUret.Server.Data;
using ITMartinUret.Server.Data.Entities;
using ITMartinUret.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.AddDbContext<UretDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Uret") ?? "Data Source=/app/data/uret.db"));

builder.Services.AddSingleton<ICaseReviewService, ClaudeCaseReviewService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<UretDbContext>().Database.EnsureCreated();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

// Attachment download — gated on the parent post still being publicly visible,
// so a hidden/deleted post's documents stop being reachable too.
app.MapGet("/vedhaeftninger/{id:guid}", async (Guid id, UretDbContext db) =>
{
    var a = await db.Attachments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    if (a is null) return Results.NotFound();

    var post = await db.Posts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == a.PostId);
    if (post is null || post.Status != PostStatus.Visible) return Results.NotFound();
    if (!File.Exists(a.StoredPath)) return Results.NotFound();

    return Results.File(a.StoredPath, "application/octet-stream", a.FileName);
});

app.Run();
