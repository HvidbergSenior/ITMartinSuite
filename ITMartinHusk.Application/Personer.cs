using System.Collections.Concurrent;
using ITMartinHusk.Domain;

namespace ITMartinHusk.Application;

// Husk for everyone (2026-11-10): one list per person. Martin keeps his own list exactly as before (husk.json, PIN);
// a customer logs in with the kundenummer and gets an own list; "Se et eksempel" opens a made-up list that lives in memory only.
public static class HuskPeople
{
    public const string Martin = "martin";
    private const string KundePrefix = "kunde-";
    private const string DemoPrefix = "eksempel-";

    public static string Kunde(string kundenummer) => KundePrefix + kundenummer;
    public static string Demo(string id) => DemoPrefix + id;

    public static bool IsDemo(string? person) => person is not null && person.StartsWith(DemoPrefix, StringComparison.Ordinal) && person.Length > DemoPrefix.Length;

    /// <summary>The 8 digits of a customer's id, or null (also for anything that is not exactly 8 digits - it names a folder).</summary>
    public static string? Kundenummer(string? person) =>
        person is not null && person.StartsWith(KundePrefix, StringComparison.Ordinal) && IsKundenummer(person[KundePrefix.Length..])
            ? person[KundePrefix.Length..] : null;

    public static bool IsKundenummer(string? s) => s is { Length: 8 } && s.All(char.IsAsciiDigit);

    public static bool IsValid(string? person) => person == Martin || IsDemo(person) || Kundenummer(person) is not null;
}

/// <summary>Who is looking - set once per request or page connection (from the cookie), read by everything else.</summary>
public sealed class HuskPerson
{
    public string Id { get; private set; } = "";

    public void Set(string id)
    {
        if (!HuskPeople.IsValid(id)) throw new ArgumentException("Ukendt person.");
        if (Id.Length > 0 && Id != id) throw new InvalidOperationException("Personen er allerede sat.");
        Id = id;
    }

    public bool IsMartin => Id == HuskPeople.Martin;
    public bool IsDemo => HuskPeople.IsDemo(Id);
    public string? Kundenummer => HuskPeople.Kundenummer(Id);
}

/// <summary>Does Kunder know this kundenummer, and is Husk switched on for customers?</summary>
public interface ICustomerCodes
{
    Task<(bool Ok, string Error)> CheckAsync(string code, CancellationToken ct = default);
}

/// <summary>Where each person's list is kept.</summary>
public interface IHuskStoreFactory
{
    IHuskStore Open(string person);
    /// <summary>Everyone with a saved list (Martin first) - the reminders go through them every minute.</summary>
    IEnumerable<string> Saved();
}

public sealed class HuskSpace(string person, HuskUseCases husk, BallUseCases balls, JobUseCases jobs)
{
    public string Person { get; } = person;
    public HuskUseCases Husk { get; } = husk;
    public BallUseCases Balls { get; } = balls;
    public JobUseCases Jobs { get; } = jobs;
    internal DateTime UsedUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Every person's own Husk, opened when first needed and kept open (the lists are small).</summary>
public sealed class HuskSpaces(IHuskStoreFactory stores, IPushSender push, Func<DateTime> now)
{
    /// <summary>An example nobody has touched for this long is forgotten.</summary>
    public static readonly TimeSpan DemoLife = TimeSpan.FromHours(3);

    private readonly ConcurrentDictionary<string, Lazy<HuskSpace>> _open = new();

    public HuskSpace For(string person)
    {
        if (!HuskPeople.IsValid(person)) throw new ArgumentException("Ukendt person.");
        var s = _open.GetOrAdd(person, p => new Lazy<HuskSpace>(() => Make(p))).Value;
        s.UsedUtc = DateTime.UtcNow;
        return s;
    }

    private HuskSpace Make(string person)
    {
        var store = stores.Open(person);
        var space = new HuskSpace(person, new HuskUseCases(store, push, now), new BallUseCases(store, now), new JobUseCases(store, now));
        if (HuskPeople.IsDemo(person)) Example.Fill(space, now());
        return space;
    }

    /// <summary>Every minute: each saved list sends its own reminders to its own phones. Examples never send anything.</summary>
    public async Task<int> TickAllAsync(CancellationToken ct = default)
    {
        var sent = 0;
        foreach (var p in stores.Saved()) sent += await For(p).Husk.TickAsync(ct);
        foreach (var (k, v) in _open)
            if (HuskPeople.IsDemo(k) && v.IsValueCreated && DateTime.UtcNow - v.Value.UsedUtc > DemoLife) _open.TryRemove(k, out _);
        return sent;
    }

    public int OpenDemos => _open.Keys.Count(HuskPeople.IsDemo);
}

/// <summary>"Se et eksempel": made-up things only, so a visitor sees how Husk works before getting an own list.</summary>
public static class Example
{
    public static void Fill(HuskSpace s, DateTime now)
    {
        var tomorrow = now.Date.AddDays(1);
        s.Husk.AddMeeting("Tid hos lægen", tomorrow.AddHours(10.25), "Lægehuset", "Sundhedskort", "Lægehuset");
        s.Husk.AddMeeting("Kaffe med Lise", now.Date.AddDays(5).AddHours(14), "Café Smagløs", "", "");
        s.Balls.Save(
        [
            new BallInput("tandlaege", "Tandlæge", "Helbred", "i gang", "Ring og bestil en tid", Deadline: now.Date.AddDays(3)),
            new BallInput("gave-mor", "Fødselsdagsgave til mor", "Familie", "i gang", "Find en gave – fx en bog", Deadline: now.Date.AddDays(6)),
            new BallInput("cykellygte", "Ny cykellygte", "Hjem", "i gang", "Køb en lygte, næste gang du handler"),
            new BallInput("kommunen-svar", "Svar fra kommunen", "Papirer", "venter", "", "Kommunen", "Ansøgningen er sendt. Husk spørger igen om en uge."),
            new BallInput("kaelder", "Ryd op i kælderen", "Hjem", "parkeret", "Tag én kasse ad gangen"),
        ]);
    }
}
