using ITMartinStarRealms.Server.Data;
using ITMartinStarRealms.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents();

var dbPath = builder.Configuration.GetConnectionString("StarRealmsDb")
    ?? "Data Source=/app/db/starrealms.db";

builder.Services.AddDbContext<StarRealmsDbContext>(o => o.UseSqlite(dbPath));

builder.Services.AddScoped<GameService>();
builder.Services.AddSingleton<StarRealmsAiService>();
builder.Services.AddHostedService<CleanupService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StarRealmsDbContext>();
    db.Database.EnsureCreated();
    await RulesetSeeder.SeedAsync(db);
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

// ── Rulesets ─────────────────────────────────────────────────────────────────

app.MapGet("/api/rulesets", async (GameService svc) =>
    Results.Ok(await svc.GetRulesetsAsync()));

app.MapPost("/api/rulesets", async (GameService svc, HttpContext ctx) =>
{
    var body = await ctx.Request.ReadFromJsonAsync<CustomRulesetBody>();
    if (body is null || string.IsNullOrWhiteSpace(body.Name)) return Results.BadRequest();
    var created = await svc.CreateCustomRulesetAsync(
        body.Name, body.Description ?? "", body.MinPlayers, body.MaxPlayers,
        body.IsTeamMode, body.PlayersPerTeam, body.SharedTeamPool, body.StartingPoints, body.CreatedByName ?? "");
    return Results.Ok(created);
});

// ── Sessions ─────────────────────────────────────────────────────────────────

app.MapPost("/api/sessions", async (GameService svc, HttpContext ctx) =>
{
    var body = await ctx.Request.ReadFromJsonAsync<CreateSessionBody>();
    if (body is null) return Results.BadRequest();
    try
    {
        var session = await svc.CreateAsync(body.RulesetId, body.StartingPoints);
        return Results.Ok(new { session.Code });
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
});

app.MapGet("/api/sessions/{code}", async (string code, GameService svc) =>
{
    var session = await svc.GetByCodeAsync(code);
    if (session is null) return Results.NotFound();

    var ruleset = (await svc.GetRulesetsAsync()).FirstOrDefault(r => r.Id == session.RulesetId);
    var events = await svc.GetRecentEventsAsync(session.Id);

    return Results.Ok(new
    {
        session.Code,
        session.RulesetName,
        RulesetDescription = ruleset?.Description ?? "",
        session.IsTeamMode,
        session.SharedTeamPool,
        session.MinPoints,
        session.MaxPoints,
        session.StartingPoints,
        session.CurrentTurnPlayerId,
        session.IsCompleted,
        Players = session.Players.OrderBy(p => p.SortOrder).Select(p => new
        {
            p.Id, p.Name, p.Avatar, p.Color, p.Points, p.Team, p.SortOrder, p.Token
        }),
        Events = events.Select(e => new { e.PlayerId, e.PlayerName, e.PlayerAvatar, e.Delta, e.ResultingPoints, e.CreatedAt })
    });
});

app.MapPost("/api/sessions/{code}/join", async (string code, GameService svc, HttpContext ctx) =>
{
    var body = await ctx.Request.ReadFromJsonAsync<JoinBody>();
    if (body is null || string.IsNullOrWhiteSpace(body.Token)) return Results.BadRequest();
    try
    {
        var player = await svc.GetOrCreatePlayerAsync(code, body.Token, body.Name ?? "", body.ProfileId, body.Avatar ?? "🚀", body.Color);
        return Results.Ok(new { player.Id, player.Name, player.Avatar, player.Color, player.Points, player.Team });
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
});

app.MapPost("/api/sessions/{code}/adjust", async (string code, GameService svc, HttpContext ctx) =>
{
    var body = await ctx.Request.ReadFromJsonAsync<AdjustBody>();
    if (body is null) return Results.BadRequest();
    try
    {
        await svc.AdjustPointsAsync(code, body.PlayerId, body.Delta);
        return Results.Ok();
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
});

app.MapPost("/api/sessions/{code}/turn", async (string code, GameService svc, HttpContext ctx) =>
{
    var body = await ctx.Request.ReadFromJsonAsync<TurnBody>();
    if (body is null) return Results.BadRequest();
    try
    {
        await svc.NextTurnAsync(code, body.PlayerId);
        return Results.Ok();
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
});

app.MapPost("/api/sessions/{code}/reset", async (string code, GameService svc) =>
{
    try
    {
        await svc.ResetAsync(code);
        return Results.Ok();
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
});

// ── Player profile (long-lived, cross-game identity) ────────────────────────

app.MapGet("/api/profile", async (string deviceToken, GameService svc) =>
{
    var profile = await svc.FindProfileAsync(deviceToken);
    return profile is null ? Results.NotFound() : Results.Ok(profile);
});

app.MapPost("/api/profile", async (GameService svc, HttpContext ctx) =>
{
    var body = await ctx.Request.ReadFromJsonAsync<ProfileBody>();
    if (body is null || string.IsNullOrWhiteSpace(body.DeviceToken)) return Results.BadRequest();
    var profile = await svc.GetOrCreateProfileAsync(body.DeviceToken, body.Name ?? "", body.Avatar ?? "🚀");
    return Results.Ok(profile);
});

app.MapGet("/api/stats", async (Guid profileId, int? sinceMonths, GameService svc) =>
{
    var since = sinceMonths is > 0 ? DateTime.UtcNow.AddMonths(-sinceMonths.Value) : (DateTime?)null;
    return Results.Ok(await svc.GetStatsAsync(profileId, since));
});

// ── AI helpers ────────────────────────────────────────────────────────────────

app.MapGet("/api/ships", () => Results.Ok(new { ShipCatalog.Factions, ShipCatalog.Ships }));

app.MapPost("/api/ai/hint", async (StarRealmsAiService ai, HttpContext ctx) =>
{
    var body = await ctx.Request.ReadFromJsonAsync<HintBody>();
    if (body is null || string.IsNullOrWhiteSpace(body.ShipName)) return Results.BadRequest();
    var text = await ai.GetShipHintAsync(body.ShipName, body.Faction ?? "");
    return Results.Ok(new { text });
});

app.MapPost("/api/ai/traderow", async (HttpContext ctx, StarRealmsAiService ai) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var file = form.Files.GetFile("file");
    if (file is null) return Results.BadRequest("No file");

    using var ms = new MemoryStream();
    await file.CopyToAsync(ms);
    var base64 = Convert.ToBase64String(ms.ToArray());
    var mime = file.ContentType.StartsWith("image/") ? file.ContentType : "image/jpeg";
    var text = await ai.AnalyzeTradeRowAsync(base64, mime);
    return Results.Ok(new { text });
});

// ── Blazor (static SSR only - no interactive render mode anywhere) ──────────

app.MapRazorComponents<ITMartinStarRealms.Server.App>();

app.Run();

record CustomRulesetBody(string Name, string? Description, int MinPlayers, int MaxPlayers, bool IsTeamMode, int PlayersPerTeam, bool SharedTeamPool, int StartingPoints, string? CreatedByName);
record CreateSessionBody(Guid RulesetId, int StartingPoints);
record JoinBody(string Token, string? Name, string? Avatar, string? Color, Guid? ProfileId);
record AdjustBody(Guid PlayerId, int Delta);
record TurnBody(Guid PlayerId);
record ProfileBody(string DeviceToken, string? Name, string? Avatar);
record HintBody(string ShipName, string? Faction);
