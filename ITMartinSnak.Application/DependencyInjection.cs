using Microsoft.Extensions.DependencyInjection;

namespace ITMartinSnak.Application;

public static class DependencyInjection
{
    /// <summary>The use cases. Ports come from AddSnakInfrastructure.</summary>
    public static IServiceCollection AddSnakApplication(this IServiceCollection services, string adminCode, string apiKey, string publicUrl)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(sp => new Rooms(sp.GetRequiredService<IRoomStore>(), sp.GetRequiredService<ILiveFeed>(),
            sp.GetRequiredService<IPushSender>(), sp.GetRequiredService<TimeProvider>(), publicUrl));
        services.AddSingleton(sp => new Calendars(sp.GetRequiredService<ICalendarSource>(), sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton(sp => new RoomMails(sp.GetRequiredService<Rooms>(), sp.GetRequiredService<IRoomMailer>(),
            sp.GetRequiredService<IPushSender>(), sp.GetRequiredService<TimeProvider>(), "Martin"));
        services.AddSingleton(sp => new IdeaIntake(sp.GetRequiredService<IPageTitles>()));
        services.AddSingleton(sp => new Access(adminCode, apiKey, sp.GetRequiredService<TimeProvider>()));
        return services;
    }
}
