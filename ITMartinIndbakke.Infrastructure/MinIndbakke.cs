using ITMartinIndbakke.Application;
using ITMartinIndbakke.Domain;
using ITMartinKunder.Client;
using ITMartinKunder.Contracts;
using MailKit.Net.Imap;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ITMartinIndbakke.Infrastructure;

// Min Indbakke (2026-11-05): the same Gmail code as Martin's, but made per login from the person's own key.
// The key is passed in and kept by the GmailImap/Unsubscriber objects in memory only - nothing here writes it anywhere.
public sealed class GmailMailAccessFactory(IHttpClientFactory http, int maxMails = 20000) : IMailAccessFactory
{
    public MailAccess Open(string email, string appPassword)
    {
        var gmail = new GmailImap(email, appPassword, maxMails);
        return new MailAccess(gmail, gmail, new Unsubscriber(http.CreateClient("unsubscribe"), email, appPassword));
    }

    public async Task<string?> CheckLoginAsync(string email, string appPassword, CancellationToken ct)
    {
        try
        {
            using var client = new ImapClient { Timeout = 30_000 };
            await client.ConnectAsync("imap.gmail.com", 993, SecureSocketOptions.SslOnConnect, ct);
            await client.AuthenticateAsync(email, appPassword.Replace(" ", ""), ct);
            await client.DisconnectAsync(true, ct);
            return null;
        }
        catch (AuthenticationException) { return "Gmail sagde nej. Tjek din Gmail-adresse og nøglen (de 16 bogstaver)."; }
        catch (Exception) when (!ct.IsCancellationRequested) { return "Vi kunne ikke nå Gmail lige nu. Prøv igen om lidt."; }
    }
}

/// <summary>/data/kunder/kunde-12345678/{opfoelgning,oprydning}.json - the person's own marks, never a key or mail text.</summary>
public sealed class JsonAccountStores(string dataDir) : IAccountStores
{
    private string Dir(string account)
    {
        var d = Path.Combine(dataDir, "kunder", account);
        Directory.CreateDirectory(d);
        return d;
    }

    public IFollowUpStore FollowUps(string account) => new JsonFollowUpStore(Path.Combine(Dir(account), "opfoelgning.json"));
    public ICleanupLog Log(string account) => new JsonCleanupLog(Path.Combine(Dir(account), "oprydning.json"));
}

/// <summary>A real kundenummer (Kunder) AND one Martin has given Min Indbakke (Indbakke__Kunder, comma separated) -
/// paid on request, so Martin decides who gets in.</summary>
public sealed class KunderCustomerCheck(KunderClient kunder, IReadOnlySet<string> allowed) : ICustomerCheck
{
    public async Task<(bool Ok, string Name, string Error)> CheckAsync(string code, CancellationToken ct)
    {
        if (!allowed.Contains(code)) return (false, "", "Min Indbakke er ikke slået til for dit nummer. Skriv til Martin, så sætter han den op.");
        var r = await kunder.CheckCodeAsync(code, KunderRoles.Market, ct);
        return (r.Ok, r.Name, r.Error);
    }
}

public static class MinIndbakkeInfrastructure
{
    public static IServiceCollection AddMinIndbakke(this IServiceCollection services, IConfiguration cfg, string dataDir)
    {
        var allowed = (cfg["Indbakke:Kunder"] ?? "").Split(',', ';', ' ')
            .Select(MinIndbakkeRules.NormalizeCode).Where(c => c.Length == 8).ToHashSet();
        services.AddKunderClient(cfg, dataDir);
        services.AddSingleton<IMailAccessFactory>(sp => new GmailMailAccessFactory(sp.GetRequiredService<IHttpClientFactory>()));
        services.AddSingleton<IAccountStores>(new JsonAccountStores(dataDir));
        services.AddSingleton<ICustomerCheck>(sp => new KunderCustomerCheck(sp.GetRequiredService<KunderClient>(), allowed));
        return services;
    }
}
