using ITMartinClub.Application;
using ITMartinClub.Domain;
using ITMartinClub.Infrastructure.External;
using ITMartinClub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinClub.Infrastructure;

public static class DependencyInjection
{
    /// <summary>The data folder (club.db, club-vapid.json, photos/): /app/data in the container, ./data locally.</summary>
    public static string DataDir(IConfiguration cfg) =>
        cfg["Club:DataDir"] ?? (Directory.Exists("/app/data") ? "/app/data" : Path.Combine(Directory.GetCurrentDirectory(), "data"));

    /// <summary>Where uploaded photos live (ClubSettings:PhotosRoot, default data/photos).</summary>
    public static string PhotosRoot(IConfiguration cfg) => cfg["ClubSettings:PhotosRoot"] ?? Path.Combine(DataDir(cfg), "photos");

    public static IServiceCollection AddClubInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        var dataDir = DataDir(cfg);
        Directory.CreateDirectory(dataDir);
        var connection = cfg.GetConnectionString("ClubDb") ?? $"Data Source={Path.Combine(dataDir, "club.db")}";
        services.AddDbContextFactory<ClubDb>(o => o.UseSqlite(connection));

        services.AddScoped(typeof(ITeamItems<>), typeof(TeamItems<>));
        services.AddScoped<ITeamStore, TeamStore>();
        services.AddScoped<IMessageStore, MessageStore>();
        services.AddScoped<ITaskStore, TaskStore>();
        services.AddScoped<ICalendarStore, CalendarStore>();
        services.AddScoped<IChatStore, ChatStore>();
        services.AddScoped<IBulletinStore, BulletinStore>();

        services.AddSingleton<IPushNotifier>(sp => new WebPushNotifier(sp.GetRequiredService<IDbContextFactory<ClubDb>>(), dataDir));
        services.AddSingleton<IMailSender, SmtpMailSender>();
        services.AddSingleton<IPhotoStore>(new FilePhotoStore(PhotosRoot(cfg)));
        return services;
    }

    /// <summary>Creates or upgrades club.db (and seeds the demo team in demo mode) - call once at start.</summary>
    public static void UpgradeClubDatabase(this IServiceProvider services, bool demoMode)
    {
        using var db = services.GetRequiredService<IDbContextFactory<ClubDb>>().CreateDbContext();
        ClubDbInitializer.Upgrade(db);
        if (demoMode) DemoSeeder.Seed(db);
    }
}
