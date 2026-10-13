using Microsoft.Extensions.DependencyInjection;

namespace ITMartinClub.Application;

public static class DependencyInjection
{
    /// <summary>The use cases. The ports come from AddClubInfrastructure.</summary>
    public static IServiceCollection AddClubApplication(this IServiceCollection services, bool demoMode)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new ClubOptions { DemoMode = demoMode });
        services.AddSingleton<AdminPinLimiter>();
        services.AddSingleton<MemberPinLimiter>();
        services.AddScoped<TeamAccess>();
        services.AddScoped<TaskBoard>();
        services.AddScoped<TeamMessages>();
        services.AddScoped<Meetings>();
        services.AddScoped<TeamCalendar>();
        services.AddScoped<TeamTalk>();
        services.AddScoped<Places>();
        services.AddScoped<TeamLists>();
        services.AddScoped<TeamMembers>();
        return services;
    }
}
