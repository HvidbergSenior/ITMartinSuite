using ITMartin.Ai;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Infrastructure.DependencyInjection;
using ITMartin.Media.Infrastructure.Persistence;
using ITMartin.Media.Runtime.Execution;
using ITMartinVlog.Server.Services;
using Microsoft.EntityFrameworkCore;

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
app.UseAntiforgery();

// Serves local video/audio files that live outside wwwroot (the user's own
// clip folders) to the <video>/<audio> players, with Range support so
// scrubbing/seeking works in the browser.
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
