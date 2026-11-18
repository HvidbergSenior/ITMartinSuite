using System.Collections.Concurrent;
using System.Text.Json;
using ITMartinIndkoeb.Application;
using ITMartinIndkoeb.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinIndkoeb.Infrastructure;

/// <summary>One JSON file per family in Indkoeb__DataDir, named by the secret key's hash (the key itself is never on disk).
/// Written to a temp file and moved, so a crash never leaves half a file.</summary>
internal sealed class JsonHusstandStore(string dir) : IHusstandStore
{
    private static readonly JsonSerializerOptions Opt = new();
    private readonly ConcurrentDictionary<string, object> _locks = new();

    private string PathFor(string noegle) =>
        Path.Combine(dir, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("indkoeb:" + noegle)))[..32].ToLowerInvariant() + ".json");

    public Husstand? Hent(string noegle)
    {
        if (string.IsNullOrWhiteSpace(noegle) || noegle.Length != 24) return null;
        var p = PathFor(noegle);
        lock (_locks.GetOrAdd(p, _ => new object()))
            return File.Exists(p) ? JsonSerializer.Deserialize<Husstand>(File.ReadAllText(p), Opt) : null;
    }

    public void Gem(Husstand h)
    {
        Directory.CreateDirectory(dir);
        var p = PathFor(h.Noegle);
        lock (_locks.GetOrAdd(p, _ => new object()))
        {
            var tmp = p + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(h, Opt));
            File.Move(tmp, p, overwrite: true);
        }
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddIndkoeb(this IServiceCollection services, IConfiguration config)
    {
        var dir = config["Indkoeb:DataDir"] ?? Path.Combine(AppContext.BaseDirectory, "data");
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IHusstandStore>(new JsonHusstandStore(dir));
        services.AddTransient<Indkoeb>();
        // A receipt read can take a while (OCR + one AI read in Kvittering).
        services.AddHttpClient<IKvitteringLaeser, KvitteringKlient>(c => c.Timeout = TimeSpan.FromSeconds(90));
        services.AddTransient<LaesKvittering>();
        return services;
    }
}
