using ITMartin.Ai;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Infrastructure.DependencyInjection;
using ITMartin.Media.Infrastructure.Persistence;
using ITMartin.Media.Infrastructure.Persistence.Stores;
using ITMartin.Media.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

    var builder = Host.CreateApplicationBuilder(args);

    // Same generic per-client derivation as ITMartinFileSorter.Server/Program.cs -
    // MediaSettings__ClientSlug is the only thing that changes between clients.
    var clientSlug = builder.Configuration["MediaSettings:ClientSlug"];
    if (!string.IsNullOrWhiteSpace(clientSlug))
    {
        builder.Configuration["MediaSettings:SourceRoot"] = $"/jobs/{clientSlug}";
        builder.Configuration["MediaSettings:LibraryRoot"] = $"/library/{clientSlug}";
    }

    // Always co-locate the media db with whatever LibraryRoot ends up being -
    // see matching comment in ITMartinFileSorter.Server/Program.cs.
    var libraryRootForDb = builder.Configuration["MediaSettings:LibraryRoot"];
    if (!string.IsNullOrWhiteSpace(libraryRootForDb))
    {
        builder.Configuration["ConnectionStrings:MediaDb"] = $"Data Source={Path.Combine(libraryRootForDb, ".media.db")}";
    }

    builder.Services.AddMediaPlatform(
        builder.Configuration);
    builder.Services.AddAi();
    builder.Services.AddFileSorterCore();
    builder.Services.AddFileSorterWorker();
    builder.Services.AddScoped<IWorkflowAlertNotifier, DbWorkflowAlertNotifier>();
  
    var libraryRoot =
        builder.Configuration[
            "MediaSettings:LibraryRoot"];

    Console.WriteLine(
        $"LIBRARY ROOT: {libraryRoot}");

    builder.Logging.ClearProviders();

    builder.Logging.AddConsole();

    builder.Logging.AddFilter(
        "Microsoft.EntityFrameworkCore",
        LogLevel.None);

    builder.Logging.AddFilter(
        "Microsoft.EntityFrameworkCore.Database.Command",
        LogLevel.None);

    // =========================
    // BUILD
    // =========================

    var host = builder.Build();

    using (var scope = host.Services.CreateScope())
    {
        var factory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<MediaDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.MigrateAsync();
    }

    Console.WriteLine(
        builder.Configuration
            .GetConnectionString("MediaDb"));
    await host.RunAsync();