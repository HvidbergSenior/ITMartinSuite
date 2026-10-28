using ITMartin.Shared.UI.Kolibri;
using ITMartinFileSorter.Gallery.Server;
using ITMartinGallery.Application;
using ITMartinGallery.Domain;
using ITMartinGallery.Infrastructure;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

// Kolibri contract only (/health, /api/hjaelp, manifest) - the gallery page itself is the
// static wwwroot/index.html with its own signed-off look, so no shared CSS or /om page here.
builder.Services.AddKolibri(k =>
{
    k.Name = "Galleri";
    k.KolibriName = "Kolibri Syn";
    k.Family = "syn";
    k.Tagline = "Familiens billeder og film - sorteret, sikret og delt med dem, der skal se dem.";
    k.Version = "2026.10";
    k.OwnOmPage = true;
    k.OwnHjaelpPage = true;
});
builder.Services.AddGallery(builder.Configuration);

var app = builder.Build();
app.MapKolibri();

var catalog = app.Services.GetRequiredService<GalleryCatalog>();
var galleries = catalog.Served;

// The view password: the gallery_<slug> cookie the login sets.
static string? Key(HttpContext ctx, Gallery g) => ctx.Request.Cookies[$"gallery_{g.Slug}"];

static IResult From<T>(Answer<T> a, Func<T, IResult> ok) => a.Outcome switch
{
    Outcome.Ok => ok(a.Value!),
    Outcome.BadRequest => Results.BadRequest(a.Message),
    _ => Results.NotFound(),
};

// Closed galleries (Galleries__N__ClosesAt passed): every way in answers "closed" - before the static files,
// so the old ?g= address is covered too. Nothing is deleted. See ClosingRule.
app.Use(async (ctx, next) =>
{
    var times = catalog.ClosingTimes;
    if (times.Count > 0)
    {
        var now = ClosingRule.NowDk();
        var slug = ClosedPage.SlugsOf(ctx.Request).FirstOrDefault(sl => times.TryGetValue(sl, out var at) && now >= at);
        if (slug is not null)
        {
            var name = catalog.FindConfigured(slug)?.Name is { Length: > 0 } n ? n : "Galleriet";
            ctx.Response.StatusCode = StatusCodes.Status410Gone;
            ctx.Response.Headers.CacheControl = "no-store";
            var path = ctx.Request.Path.Value ?? "";
            if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/libraryfiles/", StringComparison.OrdinalIgnoreCase)
                || path.Contains('.') && !path.EndsWith(".html"))
                await ctx.Response.WriteAsJsonAsync(new { closed = true, message = ClosingRule.ClosedMessage });
            else
            {
                ctx.Response.ContentType = "text/html; charset=utf-8";
                await ctx.Response.WriteAsync(ClosedPage.Html(name));
            }
            return;
        }
    }
    await next();
});

// One web-app manifest per gallery, so an installed gallery opens itself with its own name.
app.MapGet("/m/{file}", (string file) =>
{
    var slug = file.EndsWith(".webmanifest") ? file[..^".webmanifest".Length] : file;
    var s = catalog.FindConfigured(slug);
    if (s is null) return Results.NotFound();
    var name = s.Name is { Length: > 0 } n ? n : slug;
    return Results.Json(new
    {
        name, short_name = name.Length > 12 ? name[..12] : name, start_url = "/" + s.Slug, scope = "/",
        display = "standalone", background_color = "#0a0a0f", theme_color = "#0a0a0f",
        icons = new[] { new { src = "/icon-192.png", sizes = "192x192", type = "image/png" }, new { src = "/icon-512.png", sizes = "512x512", type = "image/png" } },
    }, contentType: "application/manifest+json");
});

// The warning before a gallery closes (wwwroot/closing.js shows it as a banner).
app.MapGet("/api/closing", (string gallery) =>
    catalog.ClosingTimes.TryGetValue(gallery.ToLowerInvariant(), out var at) && ClosingRule.NowDk() < at
        ? Results.Ok(new { closesAt = at.ToString("yyyy-MM-ddTHH:mm"), warning = ClosingRule.Warning(at) })
        : Results.Ok(new { }));

app.UseDefaultFiles();
app.UseStaticFiles();

var mime = new FileExtensionContentTypeProvider();
foreach (var (ext, type) in new Dictionary<string, string>
{
    [".mp4"] = "video/mp4", [".mov"] = "video/quicktime", [".mkv"] = "video/x-matroska",
    [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png", [".webp"] = "image/webp",
    [".gif"] = "image/gif", [".heic"] = "image/heic", [".avif"] = "image/avif",
    [".mp3"] = "audio/mpeg", [".m4a"] = "audio/mp4", [".aac"] = "audio/aac", [".wav"] = "audio/wav",
    [".flac"] = "audio/flac", [".ogg"] = "audio/ogg",
})
    mime.Mappings[ext] = type;

// Short links: gallery.itmartin.dk/<slug> instead of /?g=<slug>. A single path segment that
// names a gallery serves the same static page; the page reads the slug off the path.
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? "/";
    if (HttpMethods.IsGet(ctx.Request.Method) && path.Length > 1 && path.IndexOf('/', 1) < 0 && !path.Contains('.'))
    {
        var hit = catalog.FindIgnoreCase(path[1..]);
        if (hit is not null)
        {
            // StaticFiles already ran higher up the pipeline, so serve the page directly.
            // A celebration gallery gets its own timeline page instead of the browser.
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.Headers.CacheControl = "no-cache";
            var page = hit.IsCelebration ? "fejring.html" : "index.html";
            string html;
            using (var reader = new StreamReader(app.Environment.WebRootFileProvider.GetFileInfo(page).CreateReadStream()))
                html = await reader.ReadToEndAsync();
            Func<string?, string> enc = System.Net.WebUtility.HtmlEncode;
            // "Læg på telefonen" (kolibri.js): this gallery's own name and start page when installed.
            html = html.Replace("</head>",
                $"<meta name=\"application-name\" content=\"{enc(hit.Name)}\"><meta name=\"apple-mobile-web-app-title\" content=\"{enc(hit.Name)}\">" +
                $"<link rel=\"manifest\" href=\"/m/{Uri.EscapeDataString(hit.Slug)}.webmanifest\"><link rel=\"apple-touch-icon\" href=\"/apple-touch-icon.png\">\n</head>");
            if (hit.IsCelebration && ctx.RequestServices.GetRequiredService<Celebrations>().Theme(hit) is { Length: > 0 } theme)
            {
                // The theme is set before the password screen, so a themed page never shows the default look first.
                html = html.Replace("<html lang=\"da\">",
                    $"<html lang=\"da\" class=\"{enc(theme)}\" data-name=\"{enc(hit.CelebrationName ?? "")}\" data-birth=\"{enc(hit.BirthDate ?? "")}\">");
            }
            await ctx.Response.WriteAsync(html);
            return;
        }
    }
    await next();
});

// Library files: the gallery's password (cookie, or ?token= for links that cannot carry a cookie),
// then symlinks resolved by hand, then plain static files.
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? "";
    if (!path.StartsWith("/libraryfiles/", StringComparison.OrdinalIgnoreCase)) { await next(); return; }

    var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
    var g = parts.Length >= 2 ? catalog.Find(parts[1]) : null;
    if (g is null) { await next(); return; }

    if (!g.Admits(Key(ctx, g)) && !g.Admits(ctx.Request.Query["token"].ToString()))
    {
        ctx.Response.StatusCode = 401;
        return;
    }

    var (kind, target) = ctx.RequestServices.GetRequiredService<LibraryFiles>().ResolveLink(g, string.Join('/', parts.Skip(2)));
    if (kind == LibraryFiles.LinkKind.NotALink) { await next(); return; }
    if (kind == LibraryFiles.LinkKind.Missing) { ctx.Response.StatusCode = 404; return; }

    mime.TryGetContentType(target!, out var contentType);
    ctx.Response.Headers.CacheControl = "private, max-age=3600";   // never in Cloudflare's shared cache (see below)
    await Results.File(target!, contentType ?? "application/octet-stream", enableRangeProcessing: true).ExecuteAsync(ctx);
});

// Mount static files per gallery slug
var disk = app.Services.GetRequiredService<ILibraryDisk>();
foreach (var g in galleries.Where(g => disk.DirectoryExists(g.Path)))
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider        = new PhysicalFileProvider(g.Path),
        RequestPath         = $"/libraryfiles/{g.Slug}",
        ContentTypeProvider = mime,
        // "private": Cloudflare cached these for 4 h and then served them to ANYONE, password or not, and kept
        // serving an old song after it was replaced (2026-10-06). Browsers may still cache; links carry ?v=.
        OnPrepareResponse   = sf => sf.Context.Response.Headers.CacheControl = "private, max-age=3600",
    });

// ── API ───────────────────────────────────────────────────────────────────────

// Customers only ever see their own gallery, reached via a direct link - never a picker of everyone
// (SECURITY FIX 2026-08-01). The list is for Martin only, behind GalleryAdmin:Pin.
app.MapGet("/api/galleries", (string? adminPin, IConfiguration config) =>
{
    var configuredPin = config["GalleryAdmin:Pin"];
    if (string.IsNullOrEmpty(configuredPin) || adminPin != configuredPin)
        return Results.Unauthorized();
    return Results.Ok(galleries.Select(g => new { g.Slug, g.Name }));
});

// Safe unauthenticated - the caller already has to know the exact slug (from their own link).
app.MapGet("/api/gallery-info", (string gallery) =>
    catalog.Find(gallery) is { } g ? Results.Ok(new { g.Name }) : Results.NotFound());

app.MapPost("/api/login", (LoginRequest req, HttpContext ctx) =>
{
    var g = catalog.Find(req.Gallery);
    if (g is null) return Results.NotFound();
    if (!g.Admits(req.Password)) return Results.Unauthorized();
    // Lax, not Strict: a Strict cookie is not sent on a navigation that
    // starts on another site - which is every tap on a link in Messenger
    // or Mail - so people were asked to log in on every visit (2026-09-21).
    ctx.Response.Cookies.Append($"gallery_{req.Gallery}", req.Password ?? "", new CookieOptions
    {
        HttpOnly = false,
        Secure   = ctx.Request.IsHttps || ctx.Request.Headers.ContainsKey("CF-Connecting-IP"),
        SameSite = SameSiteMode.Lax,
        MaxAge   = TimeSpan.FromDays(365),
    });
    return Results.Ok();
});

app.MapPost("/api/logout", (string gallery, HttpContext ctx) =>
{
    ctx.Response.Cookies.Delete($"gallery_{gallery}");
    return Results.Ok();
});

app.MapGet("/api/celebration", (string gallery, HttpContext ctx, Celebrations celebrations) =>
{
    var g = catalog.FindIgnoreCase(gallery);
    if (g is null || !g.IsCelebration) return Results.NotFound();
    if (!g.Admits(Key(ctx, g))) return Results.Unauthorized();
    return celebrations.Page(g) is { } page ? Results.Ok(page) : Results.NotFound();
});

// The birthday page's edit mode: captions, year texts and rotations, behind a separate edit
// PIN (Galleries__N__EditPin) so guests with the view password cannot change anything.
app.MapPost("/api/celebration/edit", (string gallery, CelebrationEdit edit, HttpContext ctx, Celebrations celebrations) =>
{
    var g = catalog.FindIgnoreCase(gallery);
    if (g is null || !g.IsCelebration) return Results.NotFound();
    if (!g.AllowsEdit(ctx.Request.Headers["X-Edit-Pin"].ToString())) return Results.Unauthorized();
    if (!CelebrationRules.IsValidKey(edit.Key)) return Results.BadRequest();
    return celebrations.SaveEdit(g, edit.Kind, edit.Key, edit.Title, edit.Text, edit.Degrees) ? Results.Ok() : Results.BadRequest();
});

// Every other read below: the gallery by its exact slug, then its password.
IResult? Guard(string gallery, HttpContext ctx, out Gallery g, Func<Gallery, bool>? enabled = null)
{
    g = catalog.Find(gallery)!;
    if (g is null || enabled?.Invoke(g) == false) return Results.NotFound();
    return g.Admits(Key(ctx, g)) ? null : Results.Unauthorized();
}

app.MapGet("/api/browse", (string gallery, string? path, HttpContext ctx, FolderBrowser browser) =>
    Guard(gallery, ctx, out var g) ?? From(browser.Browse(g, path), Results.Ok));

app.MapGet("/api/summary", (string gallery, HttpContext ctx, LibrarySummary summary) =>
    Guard(gallery, ctx, out var g, x => x.ShowSummary) ?? (summary.Count(g) is { } counts ? Results.Ok(counts) : Results.NotFound()));

app.MapGet("/api/embedded-cover", (string gallery, string path, HttpContext ctx, LibraryFiles files) =>
    Guard(gallery, ctx, out var g) ?? From(files.EmbeddedCover(g, path), c => Results.Bytes(c.Data, c.MimeType)));

// The Sange gallery is where he practises away from the PC: chords + lyrics from studio.db ride along with the audio.
app.MapGet("/api/song-sheet", (string gallery, string path, HttpContext ctx, SongSheets sheets) =>
    Guard(gallery, ctx, out _) ?? (sheets.For(path) is { } sheet ? Results.Ok(sheet) : Results.NotFound()));

app.MapGet("/api/playlist", (string gallery, string folder, HttpContext ctx, CollectionsView views) =>
    Guard(gallery, ctx, out var g) ?? From(views.Playlist(g, folder), files => Results.Ok(new { files })));

app.MapGet("/api/collections", (string gallery, HttpContext ctx, CollectionsView views) =>
    Guard(gallery, ctx, out var g) ?? Results.Ok(new { collections = views.Cards(g) }));

app.MapGet("/api/collections/files", (string gallery, string name, HttpContext ctx, CollectionsView views) =>
    Guard(gallery, ctx, out var g) ?? (views.Files(g, name) is { } files ? Results.Ok(new { files }) : Results.NotFound()));

app.MapGet("/api/on-this-day", async (string gallery, HttpContext ctx, ManifestViews views) =>
    Guard(gallery, ctx, out var g, x => x.OnThisDayEnabled) ?? Results.Ok(new { years = await views.OnThisDay(g, DateTime.Today, ctx.RequestAborted) }));

app.MapGet("/api/search", async (string gallery, string q, HttpContext ctx, ManifestViews views) =>
    Guard(gallery, ctx, out var g, x => x.SearchEnabled) ?? Results.Ok(new { files = await views.Search(g, q, ctx.RequestAborted) }));

// Birthday pages read the capture date of every picture (plus pictures from other person folders) -
// a couple of minutes on the NAS. Do it right after start, so no visitor waits for it.
app.Lifetime.ApplicationStarted.Register(() => Task.Run(() =>
{
    var celebrations = app.Services.GetRequiredService<Celebrations>();
    foreach (var g in galleries.Where(x => x.IsCelebration))
    {
        try { celebrations.Build(g); }
        catch (Exception ex) { app.Logger.LogWarning(ex, "Warm-up of celebration {Slug} failed", g.Slug); }
    }
}));

app.Run();

record LoginRequest(string Gallery, string Password);
record CelebrationEdit(string Kind, string Key, string? Title, string? Text, int? Degrees);
