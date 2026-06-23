using ITMartinAdhd.Domain.Entities;
using ITMartinAdhd.Infrastructure;
using ITMartinAdhd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Environment.IsDevelopment()
    ? builder.Configuration.GetConnectionString("AdhdDb")
    : "Data Source=/app/data/adhd.db";

builder.Services.AddAdhdInfrastructure(
    builder.Configuration,
    connectionString ?? "Data Source=adhd.db");

builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AdhdDbContext>();
    db.Database.Migrate();
}

var photoDir = app.Configuration["AdhdSettings:PhotoDir"] ?? "/app/data/photos";
Directory.CreateDirectory(photoDir);

app.UseDefaultFiles();
app.UseStaticFiles();

// ── Recent items ──────────────────────────────────────────────────────────────
app.MapGet("/api/items/recent", async (AdhdDbContext db) =>
    await db.StoredItems
        .OrderByDescending(i => i.UpdatedAt)
        .Take(30)
        .Select(i => new ItemDto(i.Id, i.Name, i.Location, i.Notes, i.PhotoPath != null, i.StoredAt, i.UpdatedAt))
        .ToListAsync());

// ── Search ────────────────────────────────────────────────────────────────────
app.MapGet("/api/items/search", async (string? q, AdhdDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(q)) return Results.Ok(Array.Empty<ItemDto>());
    var lower = q.ToLower();
    var results = await db.StoredItems
        .Where(i => i.Name.ToLower().Contains(lower) ||
                    i.Location.ToLower().Contains(lower) ||
                    (i.Notes != null && i.Notes.ToLower().Contains(lower)))
        .OrderByDescending(i => i.UpdatedAt)
        .Take(20)
        .Select(i => new ItemDto(i.Id, i.Name, i.Location, i.Notes, i.PhotoPath != null, i.StoredAt, i.UpdatedAt))
        .ToListAsync();
    return Results.Ok(results);
});

// ── Add item ──────────────────────────────────────────────────────────────────
app.MapPost("/api/items", async (HttpRequest req, AdhdDbContext db, IConfiguration cfg) =>
{
    var form    = await req.ReadFormAsync();
    var name    = form["name"].ToString().Trim();
    var location = form["location"].ToString().Trim();
    var notes   = form["notes"].ToString().Trim();

    if (string.IsNullOrWhiteSpace(name)) return Results.BadRequest("Name required");

    string? photoFileName = null;
    var photo = form.Files.GetFile("photo");
    if (photo is { Length: > 0 })
    {
        var dir = cfg["AdhdSettings:PhotoDir"] ?? "/app/data/photos";
        var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
        photoFileName = Guid.NewGuid() + ext;
        await using var fs = File.Create(Path.Combine(dir, photoFileName));
        await photo.CopyToAsync(fs);
    }

    var item = new StoredItem
    {
        Name      = name,
        Location  = location,
        Notes     = string.IsNullOrEmpty(notes) ? null : notes,
        PhotoPath = photoFileName,
        StoredAt  = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };
    db.StoredItems.Add(item);
    await db.SaveChangesAsync();
    return Results.Ok(new { item.Id });
});

// ── Delete item ───────────────────────────────────────────────────────────────
app.MapDelete("/api/items/{id:int}", async (int id, AdhdDbContext db, IConfiguration cfg) =>
{
    var item = await db.StoredItems.FindAsync(id);
    if (item is null) return Results.NotFound();

    if (item.PhotoPath is not null)
    {
        var dir  = cfg["AdhdSettings:PhotoDir"] ?? "/app/data/photos";
        var path = Path.Combine(dir, item.PhotoPath);
        if (File.Exists(path)) File.Delete(path);
    }
    db.StoredItems.Remove(item);
    await db.SaveChangesAsync();
    return Results.Ok();
});

// ── Serve photos ──────────────────────────────────────────────────────────────
app.MapGet("/photos/{id:int}", async (int id, AdhdDbContext db, IConfiguration cfg) =>
{
    var item = await db.StoredItems.FindAsync(id);
    if (item?.PhotoPath is null) return Results.NotFound();
    var dir  = cfg["AdhdSettings:PhotoDir"] ?? "/app/data/photos";
    var full = Path.Combine(dir, item.PhotoPath);
    if (!File.Exists(full)) return Results.NotFound();
    var mime = Path.GetExtension(full).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png"  => "image/png",
        ".webp" => "image/webp",
        ".heic" => "image/heic",
        _       => "image/jpeg"
    };
    return Results.File(full, mime);
});

app.Run();

record ItemDto(int Id, string Name, string Location, string? Notes, bool HasPhoto, DateTime StoredAt, DateTime UpdatedAt);
