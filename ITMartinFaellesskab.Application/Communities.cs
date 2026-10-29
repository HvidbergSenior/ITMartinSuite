using System.Collections.Concurrent;
using ITMartinFaellesskab.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinFaellesskab.Application;

// Messages, members and "right now" on disk (small JSON files in Infrastructure).
public interface ICommunityStore
{
    List<Message> LoadMessages();
    void SaveMessages(IReadOnlyList<Message> messages);
    List<Member> LoadMembers();
    void SaveMembers(IReadOnlyList<Member> members);
    List<NowItem> LoadNow();
    void SaveNow(IReadOnlyList<NowItem> now);
}

/// <summary>Kunder: does this kundenummer work? Name = the name Kunder knows ("" = none). Reached = false when Kunder did not answer.</summary>
public interface ICustomerCheck
{
    Task<(bool Ok, bool Reached, string Name, string Error)> CheckAsync(string code, CancellationToken ct = default);
}

/// <summary>One free API for tips (Open Library, Scryfall, MusicBrainz, Steam).</summary>
public interface ITipSource
{
    TipKind Kind { get; }
    Task<List<Tip>> SearchAsync(string query, CancellationToken ct = default);
}

public sealed record CommunityView(Community Community, NowItem? Now, int Members, bool Joined);

// The communities. A few hundred messages a month, so everything is held in memory and written to disk on each change.
public sealed class Communities
{
    private readonly ICommunityStore _store;
    private readonly TimeProvider _clock;
    private readonly object _gate = new();
    private readonly List<Message> _messages;
    private readonly List<Member> _members;
    private readonly List<NowItem> _now;

    public Communities(ICommunityStore store, TimeProvider clock)
    {
        (_store, _clock) = (store, clock);
        _messages = store.LoadMessages();
        _members = store.LoadMembers();
        _now = store.LoadNow();
    }

    public IReadOnlyList<CommunityView> List(string? code)
    {
        lock (_gate) return CommunityRules.All.Select(c => View(c, code)).ToList();
    }

    public CommunityView? Get(string? slug, string? code)
    {
        if (CommunityRules.Find(slug) is not { } c) return null;
        lock (_gate) return View(c, code);
    }

    private CommunityView View(Community c, string? code) => new(c, _now.FirstOrDefault(n => n.Slug == c.Slug),
        _members.Count(m => m.Slugs.Contains(c.Slug)), _members.FirstOrDefault(m => m.Code == code)?.Slugs.Contains(c.Slug) == true);

    // ---- members ----

    /// <summary>Remembers the customer after a good login; a typed name wins over the one Kunder knows.</summary>
    public Member Remember(string code, string? typedName, string kunderName)
    {
        lock (_gate)
        {
            var i = _members.FindIndex(m => m.Code == code);
            var typed = CommunityRules.CleanName(typedName);
            Member m;
            if (i < 0) m = new Member(code, typed.Length > 0 ? typed : CommunityRules.CleanName(kunderName), []);
            else m = typed.Length > 0 ? _members[i] with { Name = typed }
                : _members[i].Name.Length == 0 ? _members[i] with { Name = CommunityRules.CleanName(kunderName) } : _members[i];
            if (i < 0) _members.Add(m); else _members[i] = m;
            _store.SaveMembers(_members);
            return m with { Slugs = m.Slugs.ToList() };
        }
    }

    public Member? Me(string? code)
    {
        lock (_gate) return _members.FirstOrDefault(m => m.Code == code) is { } m ? m with { Slugs = m.Slugs.ToList() } : null;
    }

    public Member? SetName(string? code, string? name)
    {
        var n = CommunityRules.CleanName(name);
        return n.Length == 0 ? null : Change(code, m => m with { Name = n });
    }

    public Member? ToggleJoin(string? code, string? slug) => Change(code, m => CommunityRules.ToggleJoin(m, slug));

    private Member? Change(string? code, Func<Member, Member?> f)
    {
        lock (_gate)
        {
            var i = _members.FindIndex(m => m.Code == code);
            if (i < 0 || f(_members[i]) is not { } changed) return null;
            _members[i] = changed;
            _store.SaveMembers(_members);
            return changed with { Slugs = changed.Slugs.ToList() };
        }
    }

    // ---- messages (logged-in customers with a name) ----

    public IReadOnlyList<Message> MessagesAfter(string? slug, long afterId, int max = 300)
    {
        lock (_gate) return _messages.Where(m => string.Equals(m.Slug, slug, StringComparison.OrdinalIgnoreCase) && m.Id > afterId).TakeLast(max).ToList();
    }

    public Message? Write(string? code, string? slug, string? text)
    {
        lock (_gate)
        {
            if (_members.FirstOrDefault(x => x.Code == code) is not { } me) return null;
            var m = CommunityRules.NewMessage(_messages, slug, me.Name, text, _clock.GetUtcNow().UtcDateTime);
            if (m is null) return null;
            _messages.Add(m);
            CommunityRules.Trim(_messages, m.Slug);
            _store.SaveMessages(_messages);
            return m;
        }
    }

    // ---- right now (Martin) ----

    public NowItem? SetNow(string? slug, string? title, string? note, string? when)
    {
        lock (_gate)
        {
            var n = CommunityRules.NewNow(slug, title, note, when, _clock.GetUtcNow().UtcDateTime);
            if (n is null) return null;
            _now.RemoveAll(x => x.Slug == n.Slug);
            _now.Add(n);
            _store.SaveNow(_now);
            return n;
        }
    }

    public bool ClearNow(string? slug)
    {
        lock (_gate)
        {
            if (_now.RemoveAll(x => string.Equals(x.Slug, slug, StringComparison.OrdinalIgnoreCase)) == 0) return false;
            _store.SaveNow(_now);
            return true;
        }
    }
}

// "Måske kan du også lide": tips from the free API that fits the community, searched on "right now" (else the start
// query). Kept 6 hours per search, so the APIs are asked rarely; an API that does not answer just gives no tips.
/// <summary>The answers, kept across requests (Tips itself is per request, like the HTTP clients it uses).</summary>
public sealed class TipCache
{
    internal readonly ConcurrentDictionary<string, (DateTime At, List<Tip> Tips)> Items = new();
}

public sealed class Tips(IEnumerable<ITipSource> sources, Communities communities, TipCache cache, TimeProvider clock)
{
    public static readonly TimeSpan Keep = TimeSpan.FromHours(6);
    private ConcurrentDictionary<string, (DateTime At, List<Tip> Tips)> _cache => cache.Items;

    public async Task<List<Tip>> ForAsync(string? slug, CancellationToken ct = default)
    {
        if (communities.Get(slug, null) is not { } v) return [];
        var q = CommunityRules.TipQuery(v.Community, v.Now);
        if (q.Length == 0 || sources.FirstOrDefault(s => s.Kind == v.Community.Tips) is not { } source) return [];
        var key = v.Community.Slug + "|" + q.ToLowerInvariant();
        var now = clock.GetUtcNow().UtcDateTime;
        if (_cache.TryGetValue(key, out var hit) && now - hit.At < Keep) return hit.Tips;
        List<Tip> found;
        try { found = (await source.SearchAsync(q, ct)).Take(6).ToList(); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException) { return []; }
        _cache[key] = (now, found);
        return found;
    }
}

public enum LoginResult { Customer, Admin, Wrong, Locked, Unreachable }

// Customers log in with the kundenummer (checked in Kunder); Martin with the admin code (Faellesskab:AdminCode).
// Ten wrong tries from one address lock it for 15 minutes.
/// <summary>Wrong tries per address, kept across requests (Access is per request, like the Kunder client).</summary>
public sealed class LoginGuard
{
    internal readonly ConcurrentDictionary<string, (int Count, DateTime First)> Wrong = new();
}

public sealed class Access(ICustomerCheck kunder, Communities communities, LoginGuard guard, string adminCode, TimeProvider clock)
{
    public const int MinAdminCode = 6, MaxWrongTries = 10;
    public static readonly TimeSpan WrongTriesWindow = TimeSpan.FromMinutes(15);
    public const string WrongCode = "Det kundenummer virker ikke. Det har 8 cifre og står i mailen fra os – har du ikke et, så skriv dig op gratis på bogshoppen.dk.";
    private ConcurrentDictionary<string, (int Count, DateTime First)> _wrong => guard.Wrong;

    public bool IsAdminCode(string? typed) => adminCode.Length >= MinAdminCode && (typed ?? "").Trim() == adminCode;

    /// <returns>The result, the kundenummer for a customer, and the reason in Danish when it went wrong.</returns>
    public async Task<(LoginResult Result, string Code, string Error)> LoginAsync(string? typed, string? name, string clientIp, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (_wrong.TryGetValue(clientIp, out var w) && w.Count >= MaxWrongTries && now - w.First < WrongTriesWindow)
            return (LoginResult.Locked, "", "For mange forkerte forsøg. Vent et kvarter og prøv igen.");
        if (IsAdminCode(typed)) { _wrong.TryRemove(clientIp, out _); return (LoginResult.Admin, "", ""); }
        if (CommunityRules.NormaliseCode(typed) is { } code)
        {
            var (ok, reached, kunderName, error) = await kunder.CheckAsync(code, ct);
            if (!reached) return (LoginResult.Unreachable, "", error.Length > 0 ? error : "Vi kan ikke tjekke dit kundenummer lige nu – prøv igen om et par minutter.");
            if (ok)
            {
                _wrong.TryRemove(clientIp, out _);
                communities.Remember(code, name, kunderName);
                return (LoginResult.Customer, code, "");
            }
        }
        _wrong.AddOrUpdate(clientIp, _ => (1, now), (_, old) => now - old.First > WrongTriesWindow ? (1, now) : (old.Count + 1, old.First));
        return (LoginResult.Wrong, "", WrongCode);
    }
}

public static class DependencyInjection
{
    /// <summary>The use cases. Ports come from AddFaellesskabInfrastructure.</summary>
    public static IServiceCollection AddFaellesskabApplication(this IServiceCollection services, string adminCode)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<Communities>();
        services.AddSingleton<TipCache>();
        services.AddScoped<Tips>();
        services.AddSingleton<LoginGuard>();
        services.AddScoped(sp => new Access(sp.GetRequiredService<ICustomerCheck>(), sp.GetRequiredService<Communities>(), sp.GetRequiredService<LoginGuard>(), adminCode,
            sp.GetRequiredService<TimeProvider>()));
        return services;
    }
}
