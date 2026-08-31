using ITMartin.Ai;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Infrastructure.DependencyInjection;
using ITMartin.Media.Infrastructure.Persistence;
using ITMartin.Media.Runtime.Execution;
using ITMartinVlog.Server.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<Microsoft.AspNetCore.Components.Server.CircuitOptions>(options =>
{
    options.DetailedErrors = true;
});

// =========================
// SERVICES
// =========================

builder.Services.AddMediaInfrastructureCore(builder.Configuration);

// Package4's render step reuses Package2's IVideoEnhancementService/
// IAudioEnhancementService (see Package4WorkflowState's own doc comment).
// Registered directly rather than via AddPackage2Pipeline, which also wires
// up Package2's own orchestrator/thumbnail/background-job surface that Vlog
// Studio never calls and that isn't fully satisfiable outside FileSorter.Server
// (e.g. IThumbnailService, Package2WorkflowRunner) - both concrete services
// below have no further DI dependencies of their own.
builder.Services.AddScoped<
    ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IVideoEnhancementService,
    ITMartin.Media.Infrastructure.Media.FfmpegVideoProcessingService>();
builder.Services.AddScoped<
    ITMartin.Media.Contracts.Contracts.Runtime.Interfaces.IAudioEnhancementService,
    ITMartin.Media.Infrastructure.Media.FfmpegAudioProcessingService>();

builder.Services.AddPackage4Pipeline(builder.Configuration);
builder.Services.AddAi();

// IWorkflowExecutor is needed by Package4WorkflowRunner, but its home
// (RuntimeDependencyInjection.AddMediaRuntime) bundles it together with
// RabbitMqBackgroundJobQueue + a queue consumer hosted service - Vlog Studio
// is a single-user local tool that calls the orchestrator/runner directly
// in-process, so pulling in a message broker just for this one interface
// would be pure overhead (and previously caused a BrokerUnreachableException
// on FileSorter.Server's own /package4-studio debug page). Registered
// standalone here instead of calling AddMediaRuntime.
builder.Services.AddScoped<IWorkflowExecutor, WorkflowExecutor>();

builder.Services.AddScoped<VlogEditorService>();
builder.Services.AddSingleton<VlogFfmpegService>();

// PIN gate: /api/media streams any local file by absolute path with no other
// auth, so this has to actually protect the raw endpoint (not just a Blazor
// page's in-circuit state) - real value comes from magic.env (Vlog__AdminPin)
// once deployed, per this repo's convention of never inlining PINs in compose.
var adminPin = builder.Configuration["Vlog:AdminPin"] ?? "1234";

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "vlog_auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.LoginPath = "/login";
        options.Events.OnRedirectToLogin = ctx =>
        {
            // /api/media is loaded by <video>/<audio>/<img> tags, not navigated
            // to - a redirect would just serve login HTML as broken media, so
            // fail cleanly with 401 instead of the default redirect-to-login.
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.None);

var app = builder.Build();

// Package4WorkflowRunner persists step/checkpoint progress via the same
// MediaDbContext tables FileSorter uses, but (unlike FileSorter.Server, which
// relies on ITMartinFileSorter.Worker having already migrated the shared db)
// Vlog Studio owns its own db file end to end, so it has to migrate it itself.
using (var scope = app.Services.CreateScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MediaDbContext>>();
    await using var db = await dbFactory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/login", (HttpContext ctx) =>
{
    var showError = ctx.Request.Query.ContainsKey("err");
    var html = $$"""
    <!doctype html><html><head><meta charset="utf-8"><title>Vlog Studio – Log ind</title>
    <style>
    body{font-family:system-ui,sans-serif;background:#111;color:#eee;display:flex;align-items:center;justify-content:center;height:100vh;margin:0}
    form{background:#1c1c1c;padding:2rem 2.5rem;border-radius:10px;box-shadow:0 4px 24px rgba(0,0,0,.4);text-align:center}
    input{font-size:1.5rem;padding:.5rem;width:8rem;text-align:center;letter-spacing:.4rem;border-radius:6px;border:1px solid #444;background:#222;color:#eee}
    button{display:block;margin:1rem auto 0;font-size:1rem;padding:.55rem 1.5rem;border-radius:6px;border:none;background:#4a9eff;color:#fff;cursor:pointer}
    .err{color:#ff6b6b;margin-top:.75rem;font-size:.9rem}
    </style></head><body>
    <form method="post" action="/login">
    <div style="margin-bottom:.75rem">🔒 Vlog Studio</div>
    <input type="password" name="pin" inputmode="numeric" autofocus autocomplete="off" />
    <button type="submit">Log ind</button>
    {{(showError ? "<div class=\"err\">Forkert PIN</div>" : "")}}
    </form></body></html>
    """;
    return Results.Content(html, "text/html");
}).AllowAnonymous();

app.MapPost("/login", async (HttpContext ctx) =>
{
    var form = await ctx.Request.ReadFormAsync();
    if (form["pin"].ToString() != adminPin)
        return Results.Redirect("/login?err=1");

    // Razor Components' antiforgery system requires every authenticated
    // identity to carry a Name claim, even for a single-user PIN gate with
    // no real accounts - throws InvalidOperationException without one.
    var claims = new[] { new Claim(ClaimTypes.Name, "vlog-admin") };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
        new AuthenticationProperties { IsPersistent = true });
    return Results.Redirect("/");
}).AllowAnonymous();

app.MapGet("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
}).AllowAnonymous();

// Serves local video/audio files that live outside wwwroot (the user's own
// clip folders) to the <video>/<audio> players, with Range support so
// scrubbing/seeking works in the browser. Gated by the cookie auth above -
// no [AllowAnonymous], so the global fallback policy protects it.
app.MapGet("/api/media", (string path) =>
{
    if (!File.Exists(path)) return Results.NotFound();

    var contentType = Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".mp4" => "video/mp4",
        ".mov" => "video/quicktime",
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/wav",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => "application/octet-stream"
    };

    return Results.File(path, contentType, enableRangeProcessing: true);
});

app.MapRazorComponents<ITMartinVlog.Server.App>()
    .AddInteractiveServerRenderMode();

app.Run();
