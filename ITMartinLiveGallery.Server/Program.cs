using ITMartinLiveGallery.Server;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<GalleryService>();
var app = builder.Build();

var mediaRoot = builder.Configuration["Gallery:MediaDir"] ?? "/media";
var adminToken = builder.Configuration["Gallery:AdminToken"] ?? "";
Directory.CreateDirectory(mediaRoot);

// Explicit UseRouting matters here: without it, WebApplication auto-inserts
// routing at the very start of the pipeline (ahead of anything below), so
// the catch-all "/{slug}" guest-page route would swallow static asset
// requests like "/style.css" before UseStaticFiles ever gets a chance -
// found this the hard way (unstyled guest page, hidden lightbox showing).
app.UseRouting();

app.UseStaticFiles(); // wwwroot: gallery.js, admin.js, style.css
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(mediaRoot),
    RequestPath = "/media",
});

bool IsAdmin(HttpRequest req) =>
    string.IsNullOrEmpty(adminToken) || req.Headers["X-Admin-Token"] == adminToken;

// ── Admin: create/list/delete events ────────────────────────────────────────

app.MapPost("/api/admin/events", (HttpRequest req, GalleryService gallery, CreateEventRequest body) =>
{
    if (!IsAdmin(req)) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(body.Slug) || string.IsNullOrWhiteSpace(body.Pin))
        return Results.BadRequest("Slug og pin er påkrævet");
    var slug = Sanitize(body.Slug);
    if (gallery.GetEvent(slug) is not null)
        return Results.Conflict("Der findes allerede et event med det navn");
    var ev = gallery.CreateEvent(slug, body.Pin, body.Title ?? body.Slug);
    return Results.Ok(new { ev.Slug, ev.Pin, ev.Title, url = $"/{ev.Slug}?pin={ev.Pin}" });
});

app.MapGet("/api/admin/events", (HttpRequest req, GalleryService gallery) =>
{
    if (!IsAdmin(req)) return Results.Unauthorized();
    return Results.Ok(gallery.AllEvents().Select(e => new
    {
        e.Slug,
        e.Pin,
        e.Title,
        e.CreatedAt,
        photoCount = gallery.GetPhotos(e.Slug).Count,
    }));
});

app.MapDelete("/api/admin/events/{slug}", (string slug, HttpRequest req, GalleryService gallery) =>
{
    if (!IsAdmin(req)) return Results.Unauthorized();
    var dir = Path.Combine(mediaRoot, Sanitize(slug));
    if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    return gallery.DeleteEvent(slug) ? Results.Ok() : Results.NotFound();
});

// ── Guest: upload + poll ─────────────────────────────────────────────────────

app.MapPost("/api/upload/{slug}", async (string slug, HttpRequest req, GalleryService gallery) =>
{
    slug = Sanitize(slug);
    var ev = gallery.GetEvent(slug);
    if (ev is null) return Results.NotFound("Ukendt event");

    if (!req.HasFormContentType) return Results.BadRequest();
    var form = await req.ReadFormAsync();
    if (form["pin"] != ev.Pin) return Results.Unauthorized();

    var file = form.Files.FirstOrDefault();
    if (file is null || file.Length == 0) return Results.BadRequest("Ingen fil");

    var mediaDir = Path.Combine(mediaRoot, slug);
    var thumbDir = Path.Combine(mediaDir, "thumbnails");
    await using var stream = file.OpenReadStream();
    var (finalName, thumbName) = await MediaProcessor.ProcessAsync(mediaDir, thumbDir, file.FileName, stream);

    var photo = gallery.AddPhoto(slug, finalName, thumbName, MediaProcessor.IsVideo(finalName), form["name"]);
    return Results.Ok(new { photo.Id });
});

app.MapGet("/api/photos/{slug}", (string slug, string? pin, GalleryService gallery) =>
{
    slug = Sanitize(slug);
    var ev = gallery.GetEvent(slug);
    if (ev is null) return Results.NotFound();
    if (ev.Pin != pin) return Results.Unauthorized();

    var photos = gallery.GetPhotos(slug).Select(p => new
    {
        p.Id,
        url = $"/media/{slug}/{p.Filename}",
        thumbUrl = string.IsNullOrEmpty(p.ThumbFilename) ? null : $"/media/{slug}/thumbnails/{p.ThumbFilename}",
        p.IsVideo,
        p.UploaderName,
        p.UploadedAt,
    });
    return Results.Ok(new { ev.Title, photos });
});

// ── Pages ─────────────────────────────────────────────────────────────────

app.MapGet("/admin", () => Results.Content(
    File.ReadAllText("wwwroot/admin.html"), "text/html; charset=utf-8"));

// Regex constraint (no dots) is the real fix, not just UseRouting ordering -
// it makes this route structurally unable to match "/style.css" or
// "/gallery.js" at all, rather than relying on subtle middleware-order
// precedence between routing and UseStaticFiles to get it right.
app.MapGet("/{slug:regex(^[a-zA-Z0-9_-]+$)}", (string slug, GalleryService gallery) =>
{
    slug = Sanitize(slug);
    if (gallery.GetEvent(slug) is null)
        return Results.Content("<h1>Ukendt event</h1>", "text/html; charset=utf-8", statusCode: 404);
    return Results.Content(File.ReadAllText("wwwroot/guest.html"), "text/html; charset=utf-8");
});

app.Run();

static string Sanitize(string slug) =>
    new(slug.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());

record CreateEventRequest(string Slug, string Pin, string? Title);
