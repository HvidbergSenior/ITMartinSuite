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
        // SQLite creates the database FILE but never its parent directory, so
        // a library root that does not exist yet crashes the whole worker at
        // startup with "SQLite Error 14: unable to open database file" - a
        // message that says nothing about the missing folder being the cause.
        //
        // That is not an edge case: it is what a first run for a new client
        // slug looks like, and what a deliberately cleaned library looks like.
        // Hit for real on 2026-09-10 after wiping TestRun5 to start the test
        // over; the worker then crash-looped until the folder was recreated by
        // hand.
        Directory.CreateDirectory(libraryRootForDb);

        // Co-locating the database with the library is the right default: it
        // keeps a library and its index together, so moving or re-pointing one
        // can never leave the other behind (a library that had genuinely been
        // indexed used to look uncached whenever LibraryRoot was overridden
        // without the connection string).
        //
        // But it is wrong when the library lives on an external drive. SQLite
        // writes constantly - WAL, checkpoints, every workflow step - and a USB
        // drive that stalls takes the whole run down with it: on 2026-09-09
        // stat() on this very mount timed out after 240s while a directory
        // listing had succeeded seconds earlier, and filesorter-web hung at
        // startup with no logs at all because its database was over there.
        //
        // So allow the database to be placed deliberately elsewhere - fast,
        // local, reliable storage - while the library itself is delivered to
        // the slow removable disk. Unset, nothing changes.
        var dbDirectory = builder.Configuration["MediaSettings:MediaDbDirectory"];

        var dbPath = string.IsNullOrWhiteSpace(dbDirectory)
            ? Path.Combine(libraryRootForDb, ".media.db")
            : Path.Combine(dbDirectory, $"{clientSlug ?? "library"}.media.db");

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        builder.Configuration["ConnectionStrings:MediaDb"] = $"Data Source={dbPath}";

        Console.WriteLine($"MEDIA DB: {dbPath}");
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