using System.Text.Json;
using ITMartinSeMed.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ITMartinSeMed.Application;

// A browser on the other end (a WebSocket in Infrastructure): it can be sent a message.
public interface IPeer : ILine
{
    Task SendAsync(string json);
}

// The help session from the server's side: who arrives, who leaves, and what the two browsers are told.
// The WebRTC handshake (offer/answer/ICE) is only passed on between them - screen and voice never come here.
public sealed class Rooms(SessionRegistry sessions, TimeProvider clock, ILogger<Rooms> log)
{
    private static string Msg(object o) => JsonSerializer.Serialize(o);

    public static bool IsPing(string text) => text == "{\"t\":\"ping\"}";
    public static bool IsEnd(string text) => text == "{\"t\":\"slut\"}";

    /// <summary>Martin's page opens: his own session again (same code) after a dropped line, otherwise a new code.</summary>
    public async Task<HelpSession> HelperArrivedAsync(IPeer helper, string? code)
    {
        var s = sessions.Resume(code, helper) ?? sessions.Create(helper);
        log.LogInformation("Se med: helper on session ({Count} open)", sessions.Count);
        await helper.SendAsync(Msg(new { t = "kode", kode = s.Code }));
        if (s.Customer is IPeer { IsOpen: true } customer)
        {
            await helper.SendAsync(Msg(new { t = "kunde-ind" }));
            await customer.SendAsync(Msg(new { t = "martin-ind" }));
        }
        return s;
    }

    /// <param name="endedOnPurpose">Martin pressed Stop - otherwise his line just dropped and he gets HelperGrace to come back.</param>
    public async Task HelperLeftAsync(HelpSession s, IPeer helper, bool endedOnPurpose)
    {
        if (endedOnPurpose)
        {
            sessions.End(s);
            await SendTo(s.Customer, new { t = "slut" });
            log.LogInformation("Se med: session ended");
            return;
        }
        sessions.LeaveHelper(s, helper);
        await SendTo(s.Customer, new { t = "martin-ud" });
        _ = Task.Delay(SessionRules.HelperGrace + TimeSpan.FromSeconds(5), clock).ContinueWith(async _ =>
        {
            if (!sessions.HelperTimedOut(s)) return;
            await SendTo(s.Customer, new { t = "slut" });
            log.LogInformation("Se med: session ended (helper did not come back)");
        });
    }

    /// <returns>The session, or null when the code is wrong/taken/old/blocked - the customer has then been told why.</returns>
    public async Task<HelpSession?> CustomerArrivedAsync(IPeer customer, string? code, string ip)
    {
        var (result, s) = sessions.Join(code, ip, customer);
        if (s is null)
        {
            var why = result switch
            {
                JoinResult.Taken => "optaget",
                JoinResult.Expired => "udloebet",
                JoinResult.TooManyTries => "ventlidt",
                _ => "ukendt",
            };
            await customer.SendAsync(Msg(new { t = "nej", hvorfor = why }));
            return null;
        }
        await customer.SendAsync(Msg(new { t = "ok" }));
        await SendTo(s.Helper, new { t = "kunde-ind" });
        return s;
    }

    public async Task CustomerLeftAsync(HelpSession s, IPeer customer)
    {
        if (!ReferenceEquals(s.Customer, customer)) return;   // already replaced by her own reconnect
        sessions.LeaveCustomer(s);
        await SendTo(s.Helper, new { t = "kunde-ud" });
    }

    /// <summary>A handshake message from one browser goes to the other one.</summary>
    public Task ForwardAsync(HelpSession s, IPeer from, string text)
    {
        var to = ReferenceEquals(from, s.Helper) ? s.Customer : s.Helper;
        return to is IPeer { IsOpen: true } p ? p.SendAsync(text) : Task.CompletedTask;
    }

    private static Task SendTo(ILine? line, object message) =>
        line is IPeer { IsOpen: true } p ? p.SendAsync(Msg(message)) : Task.CompletedTask;
}

public static class DependencyInjection
{
    public static IServiceCollection AddSeMedApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<SessionRegistry>();
        services.AddSingleton<Rooms>();
        return services;
    }
}
