using ITMartinPolstrer.Server;
using ITMartinPolstrer.Core.Data;
using ITMartinPolstrer.Core.Data.Entities;
using ITMartinPolstrer.Core.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 200 * 1024 * 1024);

var dbPath = builder.Configuration.GetConnectionString("PolstrerDb")
    ?? "Data Source=/app/data/polstrer.db";

builder.Services.AddDbContextFactory<PolstrerDbContext>(o => o.UseSqlite(dbPath));
builder.Services.AddSingleton<MediaStore>();

// Keys on the data volume, otherwise every deploy invalidates every
// antiforgery cookie already sitting in her browser.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["Polstrer:KeysDir"] ?? "/app/data/keys"))
    .SetApplicationName("polstrer");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PolstrerDbContext>>();
    using var db = factory.CreateDbContext();
    PolstrerSchema.Ensure(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();

// Photos/videos served straight from the data volume.
var media = app.Services.GetRequiredService<MediaStore>();
Directory.CreateDirectory(media.Root);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(media.Root),
    RequestPath = "/media"
});

// Single-PIN login for her; /vis/{slug} (the share page for customers and
// colleagues) and the media behind it are open, everything else is gated.
var adminPin = app.Configuration["Polstrer:AdminPin"] ?? "polstrer2026";

app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? "";
    var open = path.StartsWith("/_blazor", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/login", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/vis/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/media/", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".css", StringComparison.OrdinalIgnoreCase);
    if (open || (ctx.Request.Cookies.TryGetValue("polstrer_auth", out var v) && v == adminPin))
    {
        await next();
        return;
    }
    ctx.Response.Redirect("/login");
});

app.MapPost("/api/auth/login", (HttpContext ctx, [FromForm] string pin) =>
{
    if (pin == adminPin)
    {
        ctx.Response.Cookies.Append("polstrer_auth", adminPin, new CookieOptions
        {
            HttpOnly = true,
            Secure = ctx.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromDays(365)
        });
        return Results.Redirect("/");
    }
    return Results.Redirect("/login?error=1");
}).DisableAntiforgery();

app.MapGet("/api/auth/logout", (HttpContext ctx) =>
{
    ctx.Response.Cookies.Delete("polstrer_auth");
    return Results.Redirect("/login");
});


// ── Photo-first, connection-proof intake ────────────────────────────────────
// The phone posts each photo the moment the camera hands it back (plain
// multipart, no Blazor circuit involved - the circuit is exactly what dies
// while the camera is open). The first photo creates a draft piece; the
// details form then posts to /api/pieces and fills it in. Nothing can be
// lost after the first photo lands.
app.MapPost("/api/upload", async (HttpContext ctx, IDbContextFactory<PolstrerDbContext> dbFactory, MediaStore mediaStore) =>
{
    var form = await ctx.Request.ReadFormAsync();
    await using var db = await dbFactory.CreateDbContextAsync();

    Piece? piece = null;
    if (Guid.TryParse(form["pieceId"], out var pid))
        piece = await db.Pieces.Include(p => p.Steps).FirstOrDefaultAsync(p => p.Id == pid);
    if (piece is null)
    {
        piece = new Piece { Title = "", Slug = PieceSlug.From("moebel"), Folder = mediaStore.NewFolderFor("") };
        piece.Steps.Add(new Step { PieceId = piece.Id, Note = "Modtaget" });
        db.Pieces.Add(piece);
    }
    var step = piece.Steps.OrderBy(s => s.At).First();

    var saved = new List<object>();
    foreach (var file in form.Files)
    {
        if (file.Length == 0) continue;
        await using var stream = file.OpenReadStream();
        var m = await mediaStore.SaveAsync(piece.Folder, new MediaStore.Pending(file.FileName, stream));
        m.StepId = step.Id;
        db.Media.Add(m);
        saved.Add(new { url = MediaStore.Url(m.RelativePath), thumb = m.ThumbPath is null ? null : MediaStore.Url(m.ThumbPath), isVideo = m.IsVideo });
    }
    await db.SaveChangesAsync();
    return Results.Ok(new { pieceId = piece.Id, slug = piece.Slug, files = saved });
}).DisableAntiforgery();

// The details form. Every field is optional; a missing title is shown as
// "Uden navn" until someone fills it in on the board.
app.MapPost("/api/pieces", async (HttpContext ctx, IDbContextFactory<PolstrerDbContext> dbFactory, MediaStore mediaStore) =>
{
    var f = await ctx.Request.ReadFormAsync();
    string S(string k) => (f[k].ToString() ?? "").Trim();
    await using var db = await dbFactory.CreateDbContextAsync();

    Piece? piece = null;
    if (Guid.TryParse(f["pieceId"], out var pid))
        piece = await db.Pieces.Include(p => p.Steps).FirstOrDefaultAsync(p => p.Id == pid);
    if (piece is null)
    {
        var title = S("title");
        piece = new Piece { Slug = PieceSlug.From(string.IsNullOrEmpty(title) ? "moebel" : title), Folder = mediaStore.NewFolderFor(string.IsNullOrEmpty(title) ? "moebel" : title) };
        piece.Steps.Add(new Step { PieceId = piece.Id, Note = "Modtaget" });
        db.Pieces.Add(piece);
    }
    var newTitle = S("title");
    if (string.IsNullOrWhiteSpace(piece.Title) && !string.IsNullOrWhiteSpace(newTitle) && piece.Folder.StartsWith("Møbel"))
    {
        var media = await db.Media.Where(m => m.Step.PieceId == piece.Id).ToListAsync();
        piece.Folder = mediaStore.RenameFolderForTitle(piece.Folder, newTitle, media);
        piece.Slug = PieceSlug.From(newTitle);
    }
    piece.Title = newTitle;
    piece.Category = S("category");
    piece.Worker = S("worker");
    piece.Customer = S("customer");
    piece.Material = S("material");
    piece.Techniques = S("techniques");
    piece.InstructedBy = S("instructedBy");
    piece.Instructions = S("instructions");
    piece.Deadline = DateOnly.TryParse(S("deadline"), out var d) ? d : null;
    await db.SaveChangesAsync();
    return Results.Redirect($"/moebel/{piece.Slug}");
}).DisableAntiforgery();

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
