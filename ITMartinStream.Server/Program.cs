using ITMartinStream.Server.Components;
using ITMartinStream.Server.Models;
using ITMartinStream.Server.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

builder.Services.AddRazorComponents();
builder.Services.AddSingleton<StreamService>();

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

// ── Public project API ──────────────────────────────────────────────────────

app.MapGet("/api/project/{slug}", (string slug, StreamService stream) =>
{
    var p = stream.Get(slug);
    if (p is null) return Results.NotFound();
    return Results.Ok(new { p.Slug, p.Name, p.Emoji, p.StatusText, p.StreamUrl, p.IsActive, p.Updates });
});

app.MapPost("/api/project/{slug}/react/{id}", (string slug, Guid id, string emoji, StreamService stream) =>
{
    stream.React(slug, id, emoji);
    return Results.Ok();
});

app.MapPost("/api/project/{slug}/vote/{id}", (string slug, Guid id, int idx, StreamService stream) =>
{
    stream.VotePoll(slug, id, idx);
    return Results.Ok();
});

app.MapPost("/api/project/{slug}/comment", async (string slug, StreamService stream, HttpContext ctx) =>
{
    var comment = await ctx.Request.ReadFromJsonAsync<Comment>();
    if (comment is null || string.IsNullOrWhiteSpace(comment.Text)) return Results.BadRequest();
    comment.Id = Guid.NewGuid();
    comment.CreatedAt = DateTime.UtcNow;
    stream.SubmitComment(slug, comment);
    return Results.Ok();
});

// ── Writer API (PIN-protected) ────────────────────────────────────────────────

app.MapGet("/api/project/{slug}/writer", (string slug, string pin, StreamService stream) =>
{
    var p = stream.Get(slug);
    if (p is null) return Results.NotFound();
    if (p.WriterPin != pin) return Results.Unauthorized();
    return Results.Ok(new { p.Slug, p.Name, p.Emoji, p.StatusText, p.StreamUrl, p.IsActive, p.Updates, p.PendingComments });
});

app.MapPost("/api/project/{slug}/update", async (string slug, string pin, StreamService stream, HttpContext ctx) =>
{
    var p = stream.Get(slug);
    if (p is null) return Results.NotFound();
    if (p.WriterPin != pin) return Results.Unauthorized();
    var update = await ctx.Request.ReadFromJsonAsync<StreamUpdate>();
    if (update is null || string.IsNullOrWhiteSpace(update.Text)) return Results.BadRequest();
    update.Id = Guid.NewGuid();
    update.CreatedAt = DateTime.UtcNow;
    if (update.Reactions is null || update.Reactions.Count == 0)
        update.Reactions = new Dictionary<string, int> { ["👍"] = 0, ["🔥"] = 0, ["💡"] = 0, ["❤️"] = 0 };
    update.PollOptions ??= [];
    stream.AddUpdate(slug, update);
    return Results.Ok();
});

app.MapPost("/api/project/{slug}/status", async (string slug, string pin, StreamService stream, HttpContext ctx) =>
{
    var p = stream.Get(slug);
    if (p is null) return Results.NotFound();
    if (p.WriterPin != pin) return Results.Unauthorized();
    var body = await ctx.Request.ReadFromJsonAsync<StatusBody>();
    if (body?.Text is null) return Results.BadRequest();
    stream.UpdateStatus(slug, body.Text);
    return Results.Ok();
});

app.MapPost("/api/project/{slug}/stream-url", async (string slug, string pin, StreamService stream, HttpContext ctx) =>
{
    var p = stream.Get(slug);
    if (p is null) return Results.NotFound();
    if (p.WriterPin != pin) return Results.Unauthorized();
    var body = await ctx.Request.ReadFromJsonAsync<StatusBody>();
    stream.UpdateStreamUrl(slug, body?.Text);
    return Results.Ok();
});

app.MapPost("/api/project/{slug}/star/{id}", (string slug, Guid id, string pin, StreamService stream) =>
{
    var p = stream.Get(slug);
    if (p is null || p.WriterPin != pin) return Results.Unauthorized();
    stream.ToggleStar(slug, id);
    return Results.Ok();
});

app.MapPost("/api/project/{slug}/delete/{id}", (string slug, Guid id, string pin, StreamService stream) =>
{
    var p = stream.Get(slug);
    if (p is null || p.WriterPin != pin) return Results.Unauthorized();
    stream.DeleteUpdate(slug, id);
    return Results.Ok();
});

app.MapPost("/api/project/{slug}/approve/{id}", (string slug, Guid id, string pin, StreamService stream) =>
{
    var p = stream.Get(slug);
    if (p is null || p.WriterPin != pin) return Results.Unauthorized();
    stream.ApproveComment(slug, id);
    return Results.Ok();
});

app.MapPost("/api/project/{slug}/reject/{id}", (string slug, Guid id, string pin, StreamService stream) =>
{
    var p = stream.Get(slug);
    if (p is null || p.WriterPin != pin) return Results.Unauthorized();
    stream.RejectComment(slug, id);
    return Results.Ok();
});

app.MapPost("/api/project/{slug}/toggle", (string slug, string pin, StreamService stream) =>
{
    var p = stream.Get(slug);
    if (p is null || p.WriterPin != pin) return Results.Unauthorized();
    stream.ToggleActive(slug);
    return Results.Ok();
});

// ── Admin API ─────────────────────────────────────────────────────────────────

app.MapGet("/api/admin/projects", (string pin, StreamService stream) =>
{
    if (pin != stream.AdminPin) return Results.Unauthorized();
    return Results.Ok(stream.GetAll());
});

app.MapPost("/api/admin/projects", async (string pin, StreamService stream, HttpContext ctx) =>
{
    if (pin != stream.AdminPin) return Results.Unauthorized();
    var body = await ctx.Request.ReadFromJsonAsync<CreateProjectBody>();
    if (body is null || string.IsNullOrWhiteSpace(body.Name) || string.IsNullOrWhiteSpace(body.Slug))
        return Results.BadRequest("Name and slug required");
    if (stream.Get(body.Slug) is not null) return Results.Conflict("Slug already in use");
    stream.Create(body.Name, body.Emoji ?? "🚀", body.Slug, body.WriterPin ?? "");
    return Results.Ok();
});

app.MapPost("/api/admin/toggle/{slug}", (string slug, string pin, StreamService stream) =>
{
    if (pin != stream.AdminPin) return Results.Unauthorized();
    if (stream.Get(slug) is null) return Results.NotFound();
    stream.ToggleActive(slug);
    return Results.Ok();
});

// ── Blazor ────────────────────────────────────────────────────────────────────

app.MapRazorComponents<App>();

app.Run();

record StatusBody(string Text);
record CreateProjectBody(string Name, string? Emoji, string Slug, string? WriterPin);
