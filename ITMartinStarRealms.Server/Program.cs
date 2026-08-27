using ITMartinStarRealms.Server.Data;
using ITMartinStarRealms.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents();

var dbPath = builder.Configuration.GetConnectionString("StarRealmsDb")
    ?? "Data Source=/app/db/starrealms.db";

builder.Services.AddDbContext<StarRealmsDbContext>(o => o.UseSqlite(dbPath));

builder.Services.AddScoped<GameService>();
builder.Services.AddSingleton<ITMartinStarRealms.Server.Services.EmojiSuggestionService>();
builder.Services.AddHttpClient("fal-profile-picture");
builder.Services.AddSingleton<ITMartinStarRealms.Server.Services.ProfilePictureService>();
builder.Services.AddSingleton<ITMartinStarRealms.Server.Services.SoundService>();
builder.Services.AddSingleton<ITMartinStarRealms.Server.Services.RulesQuestionService>();
builder.Services.AddHostedService<CleanupService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StarRealmsDbContext>();
    db.Database.EnsureCreated();
    try
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE Sessions ADD COLUMN HasStarted INTEGER NOT NULL DEFAULT 0");
        // Column didn't exist before this migration - every session already in the
        // database predates the "explicit start" gate and was already being played,
        // so treat them as already started rather than retroactively locking real
        // in-progress games behind a start screen nobody will click.
        db.Database.ExecuteSqlRaw("UPDATE Sessions SET HasStarted = 1");
    }
    catch { }
    try
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE Sessions ADD COLUMN IsRanked INTEGER NOT NULL DEFAULT 1");
        db.Database.ExecuteSqlRaw("ALTER TABLE Results ADD COLUMN IsRanked INTEGER NOT NULL DEFAULT 1");
    }
    catch { }
    try
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE Profiles ADD COLUMN PinHash TEXT");
    }
    catch { }
    try
    {
        // EnsureCreated() only builds the schema for a brand-new database file -
        // it never adds tables for entities introduced after the DB already
        // exists, so a new table needs an explicit CREATE here (unlike a new
        // column on an existing table, which just needs ALTER).
        db.Database.ExecuteSqlRaw(
            "CREATE TABLE IF NOT EXISTS \"Teams\" (" +
            "\"Id\" TEXT NOT NULL CONSTRAINT \"PK_Teams\" PRIMARY KEY, " +
            "\"MemberKey\" TEXT NOT NULL, " +
            "\"Name\" TEXT NULL, " +
            "\"CreatedAt\" TEXT NOT NULL)");
        db.Database.ExecuteSqlRaw(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Teams_MemberKey\" ON \"Teams\" (\"MemberKey\")");
        db.Database.ExecuteSqlRaw("ALTER TABLE ResultPlayers ADD COLUMN TeamId TEXT");
    }
    catch { }
    await RulesetSeeder.SeedAsync(db);

    if (app.Configuration.GetValue<bool>("StarRealms:SeedDemoData"))
        await ITMartinStarRealms.Server.Data.DemoSeeder.SeedAsync(db);
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

// Explicit audio content types for recorded sounds (see SoundService) -
// the default provider maps .webm to "video/webm" and doesn't know .m4a at
// all, and Safari in particular refuses to play an <audio> src back if the
// Content-Type doesn't match an audio type it recognizes.
var audioContentTypes = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
audioContentTypes.Mappings[".webm"] = "audio/webm";
audioContentTypes.Mappings[".m4a"] = "audio/mp4";
audioContentTypes.Mappings[".aac"] = "audio/aac";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = audioContentTypes });
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
        var session = await svc.CreateAsync(body.RulesetId, body.StartingPoints, body.IsRanked);
        return Results.Ok(new { session.Code });
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
});

app.MapGet("/api/sessions/{code}", async (string code, GameService svc) =>
{
    var session = await svc.GetByCodeAsync(code);
    if (session is null) return Results.NotFound();

    var ruleset = (await svc.GetRulesetsAsync()).FirstOrDefault(r => r.Id == session.RulesetId);

    return Results.Ok(new
    {
        session.Code,
        session.RulesetName,
        RulesetDescription = ruleset?.Description ?? "",
        RulesetMaxPlayers = ruleset?.MaxPlayers ?? GameService.MaxPlayers,
        RulesetPlayersPerTeam = ruleset?.PlayersPerTeam ?? 0,
        session.IsTeamMode,
        session.SharedTeamPool,
        session.MinPoints,
        session.MaxPoints,
        session.StartingPoints,
        session.IsCompleted,
        session.HasStarted,
        Players = session.Players.OrderBy(p => p.SortOrder).Select(p => new
        {
            p.Id, p.Name, p.Avatar, p.Color, p.Points, p.Team, p.SortOrder, p.Token, p.ProfileId
        })
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

app.MapPost("/api/sessions/{code}/team", async (string code, GameService svc, HttpContext ctx) =>
{
    var body = await ctx.Request.ReadFromJsonAsync<SetTeamBody>();
    if (body is null) return Results.BadRequest();
    try
    {
        await svc.SetPlayerTeamAsync(code, body.PlayerId, body.Team);
        return Results.Ok();
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
});

app.MapPost("/api/sessions/{code}/start", async (string code, GameService svc) =>
{
    try
    {
        await svc.StartAsync(code);
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

app.MapGet("/api/profile/{id:guid}", async (Guid id, GameService svc) =>
{
    var profile = await svc.FindProfileByIdAsync(id);
    return profile is null ? Results.NotFound() : Results.Ok(profile);
});

app.MapDelete("/api/profile/{id:guid}", async (Guid id, GameService svc) =>
{
    await svc.DeleteProfileAsync(id);
    return Results.Ok();
});

app.MapPost("/api/emoji-suggestions", async (ITMartinStarRealms.Server.Services.EmojiSuggestionService svc, HttpContext ctx) =>
{
    var body = await ctx.Request.ReadFromJsonAsync<EmojiSuggestionBody>();
    var suggestions = await svc.SuggestAsync(body?.Exclude ?? []);
    return Results.Ok(suggestions);
});

app.MapPost("/api/profile-picture", async (ITMartinStarRealms.Server.Services.ProfilePictureService svc, HttpContext ctx) =>
{
    var body = await ctx.Request.ReadFromJsonAsync<ProfilePictureBody>();
    if (body is null || string.IsNullOrWhiteSpace(body.Prompt)) return Results.BadRequest();
    var url = await svc.GenerateAsync(body.Prompt);
    return url is null ? Results.Problem("Kunne ikke generere billede", statusCode: 502) : Results.Ok(new { url });
});

// Full player list for the "pick your name" picker - only ~20-40 real
// players total, cheap to return in full rather than search/autocomplete.
app.MapGet("/api/profiles", async (GameService svc) =>
    Results.Ok(await svc.GetAllProfilesAsync()));

app.MapPost("/api/profile", async (GameService svc, HttpContext ctx) =>
{
    var body = await ctx.Request.ReadFromJsonAsync<ProfileBody>();
    if (body is null || string.IsNullOrWhiteSpace(body.DeviceToken)) return Results.BadRequest();
    try
    {
        var profile = await svc.GetOrCreateProfileAsync(body.DeviceToken, body.Name ?? "", body.Avatar ?? "🚀", body.Pin);
        return Results.Ok(profile);
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
});

app.MapGet("/api/stats", async (Guid profileId, int? sinceMonths, string? ruleset, GameService svc) =>
{
    var since = sinceMonths is > 0 ? DateTime.UtcNow.AddMonths(-sinceMonths.Value) : (DateTime?)null;
    return Results.Ok(await svc.GetStatsAsync(profileId, since, ruleset));
});

app.MapGet("/api/leaderboard", async (int? sinceMonths, string? ruleset, GameService svc) =>
{
    var since = sinceMonths is > 0 ? DateTime.UtcNow.AddMonths(-sinceMonths.Value) : (DateTime?)null;
    return Results.Ok(await svc.GetLeaderboardAsync(since, ruleset));
});

// ── Teams (recurring pairs/groups in team-mode games, e.g. "ITMartin + Eigil") ──

app.MapGet("/api/leaderboard/teams", async (string ruleset, int? sinceMonths, GameService svc) =>
{
    var since = sinceMonths is > 0 ? DateTime.UtcNow.AddMonths(-sinceMonths.Value) : (DateTime?)null;
    return Results.Ok(await svc.GetTeamLeaderboardAsync(ruleset, since));
});

app.MapGet("/api/teams/for", async (string profileIds, GameService svc) =>
{
    var ids = profileIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(s => Guid.TryParse(s, out var g) ? g : (Guid?)null)
        .Where(g => g is not null).Select(g => g!.Value).Distinct().ToList();
    if (ids.Count < 2) return Results.BadRequest("Kræver mindst 2 profiler");
    return Results.Ok(await svc.GetOrCreateTeamInfoAsync(ids));
});

app.MapGet("/api/teams/mine", async (string deviceToken, GameService svc) =>
{
    var profile = await svc.FindProfileAsync(deviceToken);
    return Results.Ok(profile is null ? [] : await svc.GetMyTeamsAsync(profile.Id));
});

app.MapPost("/api/teams/{id:guid}/name", async (Guid id, GameService svc, HttpContext ctx) =>
{
    var body = await ctx.Request.ReadFromJsonAsync<TeamNameBody>();
    if (body is null || string.IsNullOrWhiteSpace(body.DeviceToken)) return Results.BadRequest();
    var profile = await svc.FindProfileAsync(body.DeviceToken);
    if (profile is null) return Results.BadRequest("Ukendt profil");
    try
    {
        await svc.RenameTeamAsync(id, body.Name ?? "", profile.Id);
        return Results.Ok();
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
});

// ── In-game rules Q&A (cheap Haiku, one call per explicit question) ─────────

app.MapPost("/api/rules-question", async (ITMartinStarRealms.Server.Services.RulesQuestionService svc, HttpContext ctx) =>
{
    var body = await ctx.Request.ReadFromJsonAsync<RulesQuestionBody>();
    if (body is null || string.IsNullOrWhiteSpace(body.Question)) return Results.BadRequest();
    var answer = await svc.AskAsync(body.RulesetName ?? "", body.RulesetDescription ?? "", body.Question);
    return answer is null
        ? Results.Problem("Kunne ikke få svar lige nu", statusCode: 502)
        : Results.Ok(new { answer });
});

// ── Custom recorded sounds (per-profile, one clip per trigger) ──────────────

app.MapGet("/api/sounds/{profileId:guid}", (Guid profileId, ITMartinStarRealms.Server.Services.SoundService svc) =>
    Results.Ok(svc.GetMine(profileId)));

app.MapPost("/api/sounds/{profileId:guid}/{trigger}", async (Guid profileId, string trigger, string? ext, HttpContext ctx, ITMartinStarRealms.Server.Services.SoundService svc) =>
{
    using var ms = new MemoryStream();
    await ctx.Request.Body.CopyToAsync(ms);
    if (ms.Length > 2_000_000) return Results.BadRequest("Optagelsen er for stor (maks 2MB)");
    try
    {
        var url = await svc.SaveAsync(profileId, trigger, ms.ToArray(), ext ?? "webm");
        return Results.Ok(new { url });
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
});

app.MapDelete("/api/sounds/{profileId:guid}/{trigger}", (Guid profileId, string trigger, ITMartinStarRealms.Server.Services.SoundService svc) =>
{
    svc.Delete(profileId, trigger);
    return Results.Ok();
});

// ── Blazor (static SSR only - no interactive render mode anywhere) ──────────

app.MapRazorComponents<ITMartinStarRealms.Server.App>();

app.Run();

record CustomRulesetBody(string Name, string? Description, int MinPlayers, int MaxPlayers, bool IsTeamMode, int PlayersPerTeam, bool SharedTeamPool, int StartingPoints, string? CreatedByName);
record CreateSessionBody(Guid RulesetId, int StartingPoints, bool IsRanked = true);
record JoinBody(string Token, string? Name, string? Avatar, string? Color, Guid? ProfileId);
record AdjustBody(Guid PlayerId, int Delta);
record ProfileBody(string DeviceToken, string? Name, string? Avatar, string? Pin);
record EmojiSuggestionBody(List<string>? Exclude);
record ProfilePictureBody(string Prompt);
record TeamNameBody(string DeviceToken, string? Name);
record SetTeamBody(Guid PlayerId, int Team);
record RulesQuestionBody(string? RulesetName, string? RulesetDescription, string Question);
