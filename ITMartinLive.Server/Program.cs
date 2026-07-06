using ITMartinLive.Server.Components;
using ITMartinLive.Server.Models;
using ITMartinLive.Server.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddHttpClient("claude");
builder.Services.AddSingleton<LiveService>();
builder.Services.AddSingleton<PushService>();
builder.Services.AddSingleton<SummaryService>();

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

// ── Push subscription API ─────────────────────────────────────────────────────

app.MapGet("/api/push/vapid-key", (PushService push) =>
    Results.Ok(new { publicKey = push.VapidPublicKey }));

app.MapPost("/api/push/subscribe", async (HttpContext ctx, PushService push, string slug) =>
{
    var req = await ctx.Request.ReadFromJsonAsync<SubscribeRequest>();
    if (req is null) return Results.BadRequest();
    push.Subscribe(slug, req.Endpoint, req.Keys.P256dh, req.Keys.Auth);
    return Results.Ok();
});

// ── Video upload API ──────────────────────────────────────────────────────────

app.MapPost("/api/upload", async (HttpContext ctx, LiveService live, string slug, string pin) =>
{
    var ev = live.Get(slug);
    if (ev is null || ev.WriterPin != pin) return Results.Unauthorized();

    var form = await ctx.Request.ReadFormAsync();
    var file = form.Files.GetFile("file");
    if (file is null) return Results.BadRequest("No file");

    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (!new[] { ".mp4", ".mov", ".webm", ".m4v" }.Contains(ext))
        return Results.BadRequest("Unsupported format");

    var mediaDir = Path.Combine("/media", slug);
    Directory.CreateDirectory(mediaDir);
    var fileName = $"{Guid.NewGuid()}{ext}";
    await using var stream = File.Create(Path.Combine(mediaDir, fileName));
    await file.CopyToAsync(stream);

    return Results.Ok(new { webPath = $"/media/{slug}/{fileName}" });
});

// ── Public event API ──────────────────────────────────────────────────────────

app.MapGet("/api/event/{slug}", (string slug, LiveService live) =>
{
    var ev = live.Get(slug);
    if (ev is null) return Results.NotFound();
    return Results.Ok(new { ev.Slug, ev.Name, ev.SportEmoji, ev.HeaderText, ev.IsActive, ev.Updates, ev.ViewerCount });
});

app.MapPost("/api/event/{slug}/react/{id}", (string slug, Guid id, string emoji, LiveService live) =>
{
    live.React(slug, id, emoji);
    return Results.Ok();
});

app.MapPost("/api/event/{slug}/vote/{id}", (string slug, Guid id, int idx, LiveService live) =>
{
    live.VotePoll(slug, id, idx);
    return Results.Ok();
});

app.MapPost("/api/event/{slug}/message", async (string slug, LiveService live, HttpContext ctx) =>
{
    var msg = await ctx.Request.ReadFromJsonAsync<ViewerMessage>();
    if (msg is null) return Results.BadRequest();
    msg.Id = Guid.NewGuid();
    msg.CreatedAt = DateTime.UtcNow;
    live.SubmitMessage(slug, msg);
    return Results.Ok();
});

// ── Writer API (PIN-protected) ────────────────────────────────────────────────

app.MapGet("/api/event/{slug}/writer", (string slug, string pin, LiveService live) =>
{
    var ev = live.Get(slug);
    if (ev is null) return Results.NotFound();
    if (ev.WriterPin != pin) return Results.Unauthorized();
    return Results.Ok(new { ev.Slug, ev.Name, ev.SportEmoji, ev.HeaderText, ev.IsActive, ev.Updates, ev.ViewerCount, ev.PendingMessages });
});

app.MapPost("/api/event/{slug}/update", async (string slug, string pin, bool? sendPush, LiveService live, PushService push, HttpContext ctx) =>
{
    var ev = live.Get(slug);
    if (ev is null) return Results.NotFound();
    if (ev.WriterPin != pin) return Results.Unauthorized();
    var update = await ctx.Request.ReadFromJsonAsync<LiveUpdate>();
    if (update is null) return Results.BadRequest();
    update.Id = Guid.NewGuid();
    update.CreatedAt = DateTime.UtcNow;
    if (update.Reactions is null || update.Reactions.Count == 0)
        update.Reactions = new Dictionary<string, int> { ["👍"] = 0, ["🔥"] = 0, ["😱"] = 0, ["😢"] = 0 };
    update.PollOptions ??= [];
    live.AddUpdate(slug, update);
    if (sendPush == true)
    {
        var title = update.Type == UpdateType.Breaking ? "🔥 BREAKING" : $"📡 {ev.Name}";
        await push.SendAsync(slug, title, update.Text);
    }
    return Results.Ok();
});

app.MapPost("/api/event/{slug}/header", async (string slug, string pin, LiveService live, HttpContext ctx) =>
{
    var ev = live.Get(slug);
    if (ev is null) return Results.NotFound();
    if (ev.WriterPin != pin) return Results.Unauthorized();
    var body = await ctx.Request.ReadFromJsonAsync<HeaderBody>();
    if (body?.Text is null) return Results.BadRequest();
    live.UpdateHeader(slug, body.Text);
    return Results.Ok();
});

app.MapPost("/api/event/{slug}/star/{id}", (string slug, Guid id, string pin, LiveService live) =>
{
    var ev = live.Get(slug);
    if (ev is null || ev.WriterPin != pin) return Results.Unauthorized();
    live.ToggleStar(slug, id);
    return Results.Ok();
});

app.MapPost("/api/event/{slug}/delete/{id}", (string slug, Guid id, string pin, LiveService live) =>
{
    var ev = live.Get(slug);
    if (ev is null || ev.WriterPin != pin) return Results.Unauthorized();
    live.DeleteUpdate(slug, id);
    return Results.Ok();
});

app.MapPost("/api/event/{slug}/approve/{id}", (string slug, Guid id, string pin, LiveService live) =>
{
    var ev = live.Get(slug);
    if (ev is null || ev.WriterPin != pin) return Results.Unauthorized();
    live.ApproveMessage(slug, id);
    return Results.Ok();
});

app.MapPost("/api/event/{slug}/reject/{id}", (string slug, Guid id, string pin, LiveService live) =>
{
    var ev = live.Get(slug);
    if (ev is null || ev.WriterPin != pin) return Results.Unauthorized();
    live.RejectMessage(slug, id);
    return Results.Ok();
});

app.MapPost("/api/event/{slug}/toggle", (string slug, string pin, LiveService live) =>
{
    var ev = live.Get(slug);
    if (ev is null || ev.WriterPin != pin) return Results.Unauthorized();
    live.ToggleActive(slug);
    return Results.Ok();
});

app.MapPost("/api/event/{slug}/push/{id}", async (string slug, Guid id, string pin, LiveService live, PushService push) =>
{
    var ev = live.Get(slug);
    if (ev is null || ev.WriterPin != pin) return Results.Unauthorized();
    var u = ev.Updates.FirstOrDefault(x => x.Id == id);
    if (u is null) return Results.NotFound();
    var title = u.Type == UpdateType.Breaking ? "🔥 BREAKING" : $"📡 {ev.Name}";
    await push.SendAsync(slug, title, u.Text);
    return Results.Ok();
});

app.MapPost("/api/event/{slug}/summary", async (string slug, string pin, LiveService live, SummaryService summary) =>
{
    var ev = live.Get(slug);
    if (ev is null || ev.WriterPin != pin) return Results.Unauthorized();
    var text = await summary.GenerateAsync(ev);
    if (string.IsNullOrEmpty(text)) return Results.Problem("Could not generate summary");
    live.AddUpdate(slug, new LiveUpdate { Type = UpdateType.Summary, Text = text });
    return Results.Ok();
});

// ── Admin API ─────────────────────────────────────────────────────────────────

app.MapGet("/api/admin/events", (string pin, LiveService live) =>
{
    if (pin != live.AdminPin) return Results.Unauthorized();
    return Results.Ok(live.GetAll());
});

app.MapPost("/api/admin/events", async (string pin, LiveService live, HttpContext ctx) =>
{
    if (pin != live.AdminPin) return Results.Unauthorized();
    var body = await ctx.Request.ReadFromJsonAsync<CreateEventBody>();
    if (body is null || string.IsNullOrWhiteSpace(body.Name) || string.IsNullOrWhiteSpace(body.Slug))
        return Results.BadRequest("Name and slug required");
    if (live.Get(body.Slug) is not null) return Results.Conflict("Slug already in use");
    live.Create(body.Name, body.Emoji ?? "🏆", body.Slug, body.WriterPin ?? "");
    return Results.Ok();
});

app.MapPost("/api/admin/toggle/{slug}", (string slug, string pin, LiveService live) =>
{
    if (pin != live.AdminPin) return Results.Unauthorized();
    var ev = live.Get(slug);
    if (ev is null) return Results.NotFound();
    live.ToggleActive(slug);
    return Results.Ok();
});

// ── Serve uploaded media ──────────────────────────────────────────────────────

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider("/media"),
    RequestPath  = "/media",
    ContentTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider
    {
        Mappings = { [".mp4"] = "video/mp4", [".mov"] = "video/quicktime", [".webm"] = "video/webm", [".m4v"] = "video/mp4" }
    }
});

// ── Blazor ────────────────────────────────────────────────────────────────────

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

record SubscribeRequest(string Endpoint, SubscribeKeys Keys);
record SubscribeKeys(string P256dh, string Auth);
record HeaderBody(string Text);
record CreateEventBody(string Name, string? Emoji, string Slug, string? WriterPin);
