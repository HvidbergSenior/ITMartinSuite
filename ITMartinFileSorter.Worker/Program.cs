using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Infrastructure.DependencyInjection;
using ITMartin.Media.Infrastructure.Persistence;
using ITMartin.Media.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddMediaPlatform(
        builder.Configuration);
    builder.Services.AddFileSorterCore();
    builder.Services.AddFileSorterWorker();
  
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