using System.Globalization;

namespace ITMartinHusk.Domain;

// Husk (user 2026-10-30): "an easy way to remember dates from AKA, kommune and so on... NOT a calendar. Something more ADHD
// friendly" - "I can only concentrate on today and maybe tomorrow... One thing at a time." New post must be read "exactly when
// it arrives", and a missed meeting must be found at once (the Talentmesse 29.10: Jobnet sent no reminder, the dagpenge for
// the day were lost). Vision: something AKA or a kommune could offer people like Martin to get the overview.
// Nothing sensitive is kept: only what the person types (a title, a time, a place) - never the letters themselves.

public enum HuskKind { Meeting, Post }

/// <summary>Open = waiting; Ready = "Jeg er klar"; Came = "Jeg kom"; Read = post read; NotThere = "Nej, jeg nåede det ikke".</summary>
public enum HuskState { Open, Ready, Came, Read, NotThere }

public sealed class HuskItem
{
    public const int MaxTitle = 120, MaxText = 200;

    public Guid Id { get; set; } = Guid.NewGuid();
    public HuskKind Kind { get; set; }
    public string Title { get; set; } = "";
    /// <summary>Danish wall-clock time of the meeting; for post: when it arrived.</summary>
    public DateTime At { get; set; }
    public string Place { get; set; } = "";
    public string Bring { get; set; } = "";
    /// <summary>Who it is from - one of the places in <see cref="HuskRules.Places"/> or free text.</summary>
    public string From { get; set; } = "";
    public HuskState State { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public bool IsOpen => State is HuskState.Open or HuskState.Ready;

    public static HuskItem Meeting(string? title, DateTime at, string? place, string? bring, string? from, DateTime now)
    {
        var t = HuskRules.Clean(title, MaxTitle);
        if (t.Length == 0) throw new ArgumentException("Skriv, hvad mødet handler om.");
        return new HuskItem
        {
            Kind = HuskKind.Meeting, Title = t, At = at, Place = HuskRules.Clean(place, MaxText), Bring = HuskRules.Clean(bring, MaxText),
            From = HuskRules.Clean(from, MaxTitle), CreatedAt = now,
        };
    }

    /// <summary>"Ny post fra Aarhus Kommune" - only who it is from, never what it says.</summary>
    public static HuskItem Post(string? from, DateTime now)
    {
        var f = HuskRules.Clean(from, MaxTitle);
        if (f.Length == 0) throw new ArgumentException("Skriv, hvem posten er fra.");
        return new HuskItem { Kind = HuskKind.Post, Title = "Ny post fra " + f, From = f, At = now, CreatedAt = now };
    }

    public void Close(HuskState state, DateTime now)
    {
        State = state;
        ClosedAt = state is HuskState.Ready ? null : now;
    }
}

/// <summary>A phone that wants the reminders.</summary>
public sealed class HuskSubscriber
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Endpoint { get; set; } = "";
    public string P256dh { get; set; } = "";
    public string Auth { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int Failures { get; set; }
}

public sealed class HuskData
{
    public List<HuskItem> Items { get; set; } = [];
    public List<HuskSubscriber> Subscribers { get; set; } = [];
    /// <summary>Reminders already sent ("itemId|kind|n"), so a restart never sends twice.</summary>
    public List<string> Sent { get; set; } = [];
    /// <summary>The day ("yyyy-MM-dd") the daily check round was last done.</summary>
    public string CheckedDay { get; set; } = "";

    /// <summary>Closed things older than 30 days and their sent-log are forgotten.</summary>
    public void Prune(DateTime now)
    {
        var gone = Items.Where(i => !i.IsOpen && i.ClosedAt is { } c && c < now.AddDays(-30)).Select(i => i.Id.ToString()).ToHashSet();
        Items.RemoveAll(i => gone.Contains(i.Id.ToString()));
        Sent.RemoveAll(s => gone.Contains(s.Split('|')[0]));
    }
}

public sealed record PushMessage(string Title, string Body, string Url = "/");

/// <summary>What the front page shows: only ONE thing, plus maybe the next one today/tomorrow.</summary>
public enum CardKind { Post, DidYouGo, Today, Tomorrow }

public sealed record HuskCard(CardKind Kind, HuskItem Item, string When);

/// <summary>A place to look in the daily check round, with where to press.</summary>
public sealed record HuskPlace(string Name, string Where, string Url);

public static class HuskRules
{
    private static readonly CultureInfo Da = new("da-DK");

    /// <summary>The places people like Martin get meetings and letters from. Data, not code: an a-kasse or kommune that
    /// offers Husk can change the list.</summary>
    public static readonly IReadOnlyList<HuskPlace> Places =
    [
        new("Jobnet", "Mit Jobnet → Kalender og møder", "https://jobnet.dk/mit-jobnet/dine-planer-og-aftaler/kalender-og-moeder"),
        new("A-kassen", "Mit AKA → Beskeder", "https://www.aka.dk/mit"),
        new("Digital Post", "Indbakken", "https://post.borger.dk"),
    ];

    /// <summary>A meeting is "passed" a quarter of an hour after it began - then Husk asks "Nåede du det?".</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromMinutes(15);

    public static string Clean(string? text, int max)
    {
        var t = string.Join(' ', (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return t.Length > max ? t[..max] : t;
    }

    /// <summary>Only today and tomorrow, one thing first: unread post, then "Nåede du …?", then today's meeting, then
    /// tomorrow's. Anything later stays hidden - the reminders bring it forward when it is close.</summary>
    public static List<HuskCard> Cards(IEnumerable<HuskItem> items, DateTime now)
    {
        var open = items.Where(i => i.IsOpen).ToList();
        var cards = new List<HuskCard>();
        cards.AddRange(open.Where(i => i.Kind == HuskKind.Post).OrderBy(i => i.At)
            .Select(i => new HuskCard(CardKind.Post, i, When(i.At, now))));
        var meetings = open.Where(i => i.Kind == HuskKind.Meeting).OrderBy(i => i.At).ToList();
        cards.AddRange(meetings.Where(i => i.At + Grace <= now).Select(i => new HuskCard(CardKind.DidYouGo, i, When(i.At, now))));
        cards.AddRange(meetings.Where(i => i.At + Grace > now && i.At.Date == now.Date).Select(i => new HuskCard(CardKind.Today, i, When(i.At, now))));
        cards.AddRange(meetings.Where(i => i.At.Date == now.Date.AddDays(1)).Select(i => new HuskCard(CardKind.Tomorrow, i, When(i.At, now))));
        return cards;
    }

    /// <summary>"om 40 minutter", "i dag kl. 9.30", "i morgen kl. 9.30", "for 2 timer siden", "i går kl. 10.30", "på fredag kl. 9.30".</summary>
    public static string When(DateTime at, DateTime now)
    {
        var time = at.ToString("H.mm", Da);
        var ahead = at - now;
        if (ahead > TimeSpan.Zero && ahead < TimeSpan.FromHours(2))
            return ahead.TotalMinutes < 60 ? $"om {Math.Max(1, (int)Math.Ceiling(ahead.TotalMinutes))} minutter" : $"om 1 time og {(int)ahead.TotalMinutes - 60} minutter";
        if (ahead <= TimeSpan.Zero && ahead > TimeSpan.FromHours(-12) && at.Date == now.Date)
        {
            var ago = now - at;
            return ago.TotalMinutes < 60 ? $"for {Math.Max(1, (int)ago.TotalMinutes)} minutter siden" : $"i dag kl. {time}";
        }
        var days = (at.Date - now.Date).Days;
        return days switch
        {
            0 => $"i dag kl. {time}",
            1 => $"i morgen kl. {time}",
            -1 => $"i går kl. {time}",
            > 1 and < 7 => $"på {at.ToString("dddd", Da)} kl. {time}",
            _ => $"{at.ToString("d. MMMM", Da)} kl. {time}",
        };
    }

    /// <summary>The daily check round is due once a day from 8 in the morning until it is done.</summary>
    public static bool CheckRoundDue(string checkedDay, DateTime now) => now.Hour >= 8 && checkedDay != Day(now);

    public static string Day(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Post nags every half hour, in the daytime, until it is read - at most this many times.</summary>
    public const int MaxPostNags = 20;
    public static readonly TimeSpan PostEvery = TimeSpan.FromMinutes(30);

    /// <summary>The reminders due now: (sent-log key, message). A reminder time from before the thing was added is skipped,
    /// so adding tomorrow's meeting at 21 does not fire last evening's reminder.</summary>
    public static List<(string Key, PushMessage Message)> Due(IEnumerable<HuskItem> items, DateTime now, ICollection<string> sent)
    {
        var due = new List<(string, PushMessage)>();
        foreach (var i in items.Where(x => x.IsOpen))
        {
            if (i.Kind == HuskKind.Post)
            {
                if (now.Hour is < 8 or >= 21 && i.At.AddMinutes(2) < now) continue;   // the first one goes at once, also at night
                var n = (int)((now - i.At) / PostEvery);
                if (n < 0 || n >= MaxPostNags) continue;
                Add($"{i.Id}|post|{n}", new PushMessage(n == 0 ? i.Title : $"Husk: {i.Title}", "Læs den nu – tryk her, og tryk Læst bagefter.", "/"));
                continue;
            }
            var where = i.Place.Length > 0 ? $" – {i.Place}" : "";
            var bring = i.Bring.Length > 0 ? $" Husk: {i.Bring}." : "";
            Try("aften", i.At.Date.AddDays(-1).AddHours(19), new($"I morgen: {i.Title}", $"Kl. {i.At.ToString("H.mm", Da)}{where}.{bring}"));
            Try("morgen", i.At.Date.AddHours(7.5), new($"I dag: {i.Title}", $"Kl. {i.At.ToString("H.mm", Da)}{where}.{bring}"));
            Try("time", i.At.AddHours(-1), new($"Om en time: {i.Title}", $"Kl. {i.At.ToString("H.mm", Da)}{where}. Tag af sted nu, hvis du skal nå det."));
            if (i.At + Grace <= now && !sent.Contains($"{i.Id}|spurgt"))
                Add($"{i.Id}|spurgt", new($"Nåede du det? {i.Title}", "Tryk her. Nåede du det ikke, så skriv eller ring i dag – så mister du kun den ene dag."));

            void Try(string kind, DateTime at, PushMessage msg)
            {
                if (at > now || at < i.CreatedAt || now >= i.At) return;
                Add($"{i.Id}|{kind}", msg);
            }
        }
        return due;

        void Add(string key, PushMessage msg)
        {
            if (!sent.Contains(key)) due.Add((key, msg));
        }
    }
}
