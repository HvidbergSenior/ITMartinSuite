using ITMartinFamily.Application.Interfaces;
using ITMartinFamily.Infrastructure.Repositories;
using ITMartinFamily.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinFamily.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddFamilyInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=/app/data/family.db";

        services.AddDbContext<FamilyDbContext>(o => o.UseSqlite(connectionString));
        services.AddScoped<IDailyTaskRepository, DailyTaskRepository>();
        services.AddScoped<IPersonalReminderRepository, PersonalReminderRepository>();
        services.AddScoped<IFamilyStoredItemRepository, FamilyStoredItemRepository>();
        services.AddScoped<IPushSubscriptionRepository, PushSubscriptionRepository>();
        services.AddScoped<IPushNotificationService, PushNotificationService>();
        services.AddScoped<IFamilyClaudeService, FamilyClaudeService>();
        services.AddSingleton<VapidKeyProvider>();

        return services;
    }
}
