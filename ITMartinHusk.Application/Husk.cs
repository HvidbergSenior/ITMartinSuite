using ITMartinHusk.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinHusk.Application;

// The one list and the phones that want reminders - one JSON file (like ElPriser's subscribers.json).
public interface IHuskStore
{
    HuskData Get();
    void Update(Action<HuskData> change);
}

public interface IPushSender
{
    string PublicKey { get; }
    /// <summary>False when the phone is gone (it is then removed).</summary>
    Task<bool> SendAsync(HuskSubscriber to, PushMessage msg, CancellationToken ct = default);
}

public sealed record PushSignup(string Endpoint, string P256dh, string Auth);

public sealed record HuskToday(List<HuskCard> Cards, bool CheckRoundDue);

/// <summary>Husk: one thing at a time, reminders on the phone, post until read, "Nåede du det?" after a meeting.</summary>
public sealed class HuskUseCases(IHuskStore store, IPushSender push, Func<DateTime> now)
{
    public HuskToday Today()
    {
        var d = store.Get();
        var t = now();
        return new HuskToday(HuskRules.Cards(d.Items, t), HuskRules.CheckRoundDue(d.CheckedDay, t));
    }

    /// <summary>Null when saved, else the reason in plain Danish.</summary>
    public string? AddMeeting(string? title, DateTime at, string? place, string? bring, string? from)
    {
        try
        {
            var item = HuskItem.Meeting(title, at, place, bring, from, now());
            store.Update(d => d.Items.Add(item));
            return null;
        }
        catch (ArgumentException e) { return e.Message; }
    }

    /// <summary>"Ny post fra …": the first reminder goes out at the next tick (within a minute).</summary>
    public string? AddPost(string? from)
    {
        try
        {
            var item = HuskItem.Post(from, now());
            store.Update(d => d.Items.Add(item));
            return null;
        }
        catch (ArgumentException e) { return e.Message; }
    }

    public void Set(Guid id, HuskState state) =>
        store.Update(d => d.Items.FirstOrDefault(i => i.Id == id)?.Close(state, now()));

    public void Remove(Guid id) => store.Update(d => d.Items.RemoveAll(i => i.Id == id));

    public void CheckRoundDone() => store.Update(d => d.CheckedDay = HuskRules.Day(now()));

    /// <summary>Everything still open, soonest first - "Alt" behind a button, for the times you need to look ahead.</summary>
    public List<HuskItem> AllOpen() => store.Get().Items.Where(i => i.IsOpen).OrderBy(i => i.At).ToList();

    /// <summary>Seeds the first dates once (Martin's own, 2026-10-30) - only when the list has never had anything.</summary>
    public void SeedOnce(IEnumerable<(string Title, DateTime At, string Place, string From)> first)
    {
        if (store.Get().Items.Count > 0) return;
        foreach (var (title, at, place, from) in first) AddMeeting(title, at, place, "", from);
    }

    // ── phones ──
    public string PublicKey => push.PublicKey;

    public void Subscribe(PushSignup s)
    {
        if (string.IsNullOrWhiteSpace(s.Endpoint)) return;
        store.Update(d =>
        {
            var sub = d.Subscribers.FirstOrDefault(x => x.Endpoint == s.Endpoint);
            if (sub is null) d.Subscribers.Add(sub = new HuskSubscriber { Endpoint = s.Endpoint });
            sub.P256dh = s.P256dh;
            sub.Auth = s.Auth;
        });
    }

    public void Unsubscribe(string endpoint) => store.Update(d => d.Subscribers.RemoveAll(x => x.Endpoint == endpoint));

    public bool IsSubscribed(string endpoint) => store.Get().Subscribers.Any(x => x.Endpoint == endpoint);

    public async Task<bool> TestAsync(string endpoint, CancellationToken ct = default) =>
        store.Get().Subscribers.FirstOrDefault(x => x.Endpoint == endpoint) is { } s
        && await push.SendAsync(s, new PushMessage("Husk virker", "Sådan ser en påmindelse ud. Du får dem aftenen før, om morgenen og en time før."), ct);

    /// <summary>Every minute: sends what is due to every phone and remembers it, so nothing is sent twice.</summary>
    public async Task<int> TickAsync(CancellationToken ct = default)
    {
        var d = store.Get();
        var due = HuskRules.Due(d.Items.ToList(), now(), d.Sent.ToHashSet());
        if (due.Count == 0) return 0;
        // Marked as sent first: a phone that is off must not get the same message 60 times when it comes back.
        store.Update(x => x.Sent.AddRange(due.Select(m => m.Key)));
        foreach (var (_, msg) in due)
            foreach (var s in store.Get().Subscribers.ToList())
                await push.SendAsync(s, msg, ct);
        store.Update(x => x.Prune(now()));
        return due.Count;
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddHuskApplication(this IServiceCollection services) =>
        services.AddSingleton(sp => new HuskUseCases(sp.GetRequiredService<IHuskStore>(), sp.GetRequiredService<IPushSender>(), () => DkTime.Now))
            .AddSingleton(sp => new BallUseCases(sp.GetRequiredService<IHuskStore>(), () => DkTime.Now));
}
