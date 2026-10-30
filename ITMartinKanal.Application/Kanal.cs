using ITMartinKanal.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinKanal.Application;

public interface IKanalStore
{
    KanalData Get();
    void Update(Action<KanalData> change);
}

/// <summary>The channels set up in config (Kanal__Channels__0__Slug / __Title / __Intro / __Topics__0 …).</summary>
public sealed class KanalChannels(IEnumerable<KanalChannel> channels)
{
    private readonly List<KanalChannel> _all = channels.Where(c => c.Slug.Length > 0).ToList();
    public KanalChannel? Find(string? slug) => _all.FirstOrDefault(c => c.Slug == (slug ?? "").Trim().ToLowerInvariant());
    public IReadOnlyList<KanalChannel> All => _all;
}

public sealed class KanalUseCases(IKanalStore store, KanalChannels channels, Func<DateTime> now)
{
    public KanalChannel? Channel(string? slug) => channels.Find(slug);

    public bool Asleep => KanalRules.Asleep(KanalRules.DanishNow(now()));

    public List<KanalComment> Comments(string channel) => KanalRules.For(store.Get().Comments, channel);

    /// <summary>Null when saved, else the reason in plain Danish.</summary>
    public string? Write(string channelSlug, string? topic, string? name, string? text)
    {
        if (channels.Find(channelSlug) is not { } ch) return "Kanalen findes ikke.";
        if (Asleep) return "Siden sover fra kl. 23 til kl. 7 – kom igen i morgen tidlig.";
        if (KanalRules.Full(store.Get().Comments, ch.Slug, now())) return "Der er skrevet meget i dag – prøv igen i morgen.";
        try
        {
            var c = KanalComment.Write(ch, topic, name, text, now());
            store.Update(d => d.Comments.Add(c));
            return null;
        }
        catch (ArgumentException e) { return e.Message; }
    }

    /// <summary>Martin only (checked by the page).</summary>
    public void Delete(Guid id) => store.Update(d => d.Comments.RemoveAll(c => c.Id == id));
}

public static class DependencyInjection
{
    public static IServiceCollection AddKanalApplication(this IServiceCollection services, IEnumerable<KanalChannel> channels, Func<DateTime> now)
    {
        services.AddSingleton(new KanalChannels(channels));
        services.AddSingleton(sp => new KanalUseCases(sp.GetRequiredService<IKanalStore>(), sp.GetRequiredService<KanalChannels>(), now));
        return services;
    }
}
