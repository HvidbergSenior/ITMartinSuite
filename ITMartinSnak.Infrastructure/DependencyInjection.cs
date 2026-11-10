using ITMartinSnak.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ITMartinSnak.Infrastructure;

public static class DependencyInjection
{
    /// <summary>JSON files per room, live feed and WebPush - all in the data folder (Snak:DataDir, default /data).</summary>
    public static IServiceCollection AddSnakInfrastructure(this IServiceCollection services, string dataDir, SmtpSettings? smtp = null)
    {
        services.AddSingleton<IRoomStore>(sp => new JsonRoomStore(dataDir, sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<ILiveFeed, SseLiveFeed>();
        services.AddSingleton<ICalendarSource, HttpCalendarSource>();
        services.AddSingleton<IPushSender>(sp => new WebPushSender(dataDir, sp.GetRequiredService<ILogger<WebPushSender>>()));
        services.AddSingleton<IRoomMailer>(sp => new SmtpRoomMailer(smtp ?? new SmtpSettings("send.one.com", 587, "info@itmartin.dk", null, "ITMartin@Mensa.dk"),
            sp.GetRequiredService<ILogger<SmtpRoomMailer>>()));
        return services;
    }
}
