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
    /// <summary>Gone = the phone said no for good (the caller removes it).</summary>
    Task<PushResult> SendAsync(HuskSubscriber to, PushMessage msg, CancellationToken ct = default);
}

public enum PushResult { Sent, Gone, Failed }

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

    // ── Ugens jobsøgning (Regler) ──
    public int JobsPerWeek => store.Get().JobsPerWeek;

    /// <summary>The person's own number from "Krav til jobsøgning"; 0 turns the weekly reminder off.</summary>
    public void SetJobsPerWeek(int n) => store.Update(d => d.JobsPerWeek = Math.Clamp(n, 0, 50));

    /// <summary>The week the reminder is about now (this week from Friday, else last week), or null when nothing is due.</summary>
    public DateTime? JobLogWeek()
    {
        var d = store.Get();
        return RuleBook.JobLogDue(d.JobsPerWeek, d.JobLogDone, now()) ? RuleBook.JobWeekMonday(now()) : null;
    }

    public void JobLogWritten() => store.Update(d =>
    {
        var w = RuleBook.WeekKey(RuleBook.JobWeekMonday(now()));
        if (!d.JobLogDone.Contains(w)) d.JobLogDone.Add(w);
        if (d.JobLogDone.Count > 60) d.JobLogDone.RemoveRange(0, d.JobLogDone.Count - 60);
    });

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
        && await SendAsync(s, new PushMessage("Husk virker", "Sådan ser en påmindelse ud. Du får dem aftenen før, om morgenen og en time før – og for bolde med frist: aftenen før og om morgenen på dagen."), ct);

    /// <summary>Every minute: sends what is due to every phone and remembers it, so nothing is sent twice.</summary>
    public async Task<int> TickAsync(CancellationToken ct = default)
    {
        var d = store.Get();
        var sentSet = d.Sent.ToHashSet();
        var due = HuskRules.Due(d.Items.ToList(), now(), sentSet);
        due.AddRange(RuleBook.JobLogPushes(d.JobsPerWeek, d.JobLogDone, now(), sentSet));
        due.AddRange(BallRules.Reminders(d.Balls.ToList(), now(), sentSet));
        if (due.Count == 0) return 0;
        // Marked as sent first: a phone that is off must not get the same message 60 times when it comes back.
        store.Update(x => x.Sent.AddRange(due.Select(m => m.Key)));
        foreach (var (_, msg) in due)
            foreach (var s in store.Get().Subscribers.ToList())
                await SendAsync(s, msg, ct);
        store.Update(x => x.Prune(now()));
        return due.Count;
    }

    /// <summary>One message to one phone; a phone that is gone is removed from THIS person's list.</summary>
    private async Task<bool> SendAsync(HuskSubscriber s, PushMessage msg, CancellationToken ct)
    {
        var r = await push.SendAsync(s, msg, ct);
        if (r == PushResult.Gone) store.Update(d => d.Subscribers.RemoveAll(x => x.Id == s.Id));
        else if (r == PushResult.Failed) store.Update(d => { if (d.Subscribers.FirstOrDefault(x => x.Id == s.Id) is { } sub) sub.Failures++; });
        return r == PushResult.Sent;
    }
}

public static class DependencyInjection
{
    /// <summary>One Husk per person: the use cases a page or request gets are those of the person in <see cref="HuskPerson"/>.</summary>
    public static IServiceCollection AddHuskApplication(this IServiceCollection services) =>
        services.AddSingleton(sp => new HuskSpaces(sp.GetRequiredService<IHuskStoreFactory>(), sp.GetRequiredService<IPushSender>(), () => DkTime.Now))
            .AddScoped<HuskPerson>()
            .AddScoped(sp => Space(sp).Husk)
            .AddScoped(sp => Space(sp).Balls)
            .AddScoped(sp => Space(sp).Jobs);

    private static HuskSpace Space(IServiceProvider sp) =>
        sp.GetRequiredService<HuskSpaces>().For(sp.GetRequiredService<HuskPerson>().Id);
}
