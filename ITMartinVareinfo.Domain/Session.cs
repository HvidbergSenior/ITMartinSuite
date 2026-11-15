namespace ITMartinVareinfo.Domain;

/// <summary>A scanning session (user 2026-10-29: "it should be Session 1, 2, 3, etc... A session should be named
/// something like location and/or intent" - "so remove all that has to do with boxes"). Martin starts it, the helpers
/// tap it and take photos - many helpers in the same session at once. A product is found again by the session title:
/// "Session 3 – Boxit, reol 2".</summary>
public sealed class WorkSession
{
    /// <summary>Lager still keeps things per box code (until Lager gets sessions too): session N goes to Lager as
    /// K(5000+N), far above the numbers Lager writes on its own boxes, below the personal test codes (9000+).</summary>
    public const int FirstCode = 5000;
    public const int MaxNumber = BoxCodes.FirstTest - FirstCode - 1;

    public int Id { get; set; }
    /// <summary>1, 2, 3 ... - the next after the highest so far. 0 = a personal practice session.</summary>
    public int Number { get; set; }
    /// <summary>Place and/or purpose: "Boxit, reol 2", "Loppemarked lørdag". May be empty.</summary>
    public string Name { get; set; } = "";
    /// <summary>The code Lager keeps the photos under (K5003). Never shown in Vareinfo.</summary>
    public string BoxCode { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    /// <summary>The helper this session is for (Taker.Id - a kundenummer helper is minus the number). Null = every helper.</summary>
    public int? HelperId { get; set; }
    public string HelperName { get; set; } = "";
    /// <summary>Fund / Tjek (user 2026-11-03: "The pictures today are NOT products from our storage") - only old rows; Vareinfo
    /// makes no new ones since 2026-11-15 ("i want an app for only tjek"). Lager still gets the flag for them.</summary>
    public bool Fund { get; set; }
    /// <summary>The button on the start screen (CaptureKinds: "Blade og album", "Blandet" ...) - user 2026-11-10: "These sessions
    /// could be all things magazines, strikke, ting, etc" + "maybe automatic session on days". "" = a session Martin started by hand.</summary>
    public string Kind { get; set; } = "";

    public bool Open => ClosedAt is null;
    public bool IsPractice => BoxCodes.IsTest(BoxCode);

    /// <summary>A day session (made by a button, one per day per button) - its photos are read at once.</summary>
    public bool IsDaily => Kind != "";

    /// <summary>"Blade og album – 10. november", "Session 3 – Boxit, reol 2", "Session 3", or "Øvesession – Rico".</summary>
    public string Title => IsPractice ? $"Øvesession – {HelperName}" : IsDaily && Name != "" ? Name
        : Name == "" ? $"Session {Number}" : $"Session {Number} – {Name}";

    public static WorkSession Start(int number, string? name, DateTime now, string kind = "")
    {
        if (number is < 1 or > MaxNumber) throw new InvalidOperationException("Der er ikke flere sessionsnumre.");
        return new() { Number = number, Name = Clean(name), BoxCode = BoxCodes.For(FirstCode + number), CreatedAt = now, Kind = Clean(kind) };
    }

    /// <summary>Today's session of a button, by Danish date: "Blade og album – 10. november".</summary>
    public static string TodayName(string kind, DateTime utcNow)
    {
        var dk = TimeZoneInfo.ConvertTimeFromUtc(utcNow, Copenhagen);
        return Clean(kind) + " – " + dk.ToString("d. MMMM", System.Globalization.CultureInfo.GetCultureInfo("da-DK"));
    }

    /// <summary>Is this today's session of that button (a Mine varer session, same button, open, made today in Danish time)?</summary>
    public bool IsToday(string kind, DateTime utcNow) =>
        Open && !IsPractice && !Fund && Kind == Clean(kind) && SameDay(CreatedAt, utcNow);

    /// <summary>A day session from an earlier day - it closes by itself (no one has to press Luk).</summary>
    public bool IsOldDaily(DateTime utcNow) => Open && IsDaily && !SameDay(CreatedAt, utcNow);

    private static bool SameDay(DateTime a, DateTime b) =>
        TimeZoneInfo.ConvertTimeFromUtc(a, Copenhagen).Date == TimeZoneInfo.ConvertTimeFromUtc(b, Copenhagen).Date;

    private static readonly TimeZoneInfo Copenhagen = FindCopenhagen();
    private static TimeZoneInfo FindCopenhagen()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time"); }
    }

    /// <summary>The next number: one more than the highest so far.</summary>
    public static int NextNumber(IEnumerable<int> used) => used.DefaultIfEmpty(0).Max() + 1;

    /// <summary>Martin sees every session; a helper the sessions given to them, or the ones for everybody.</summary>
    public bool IsFor(Taker me) => IsPractice ? HelperId == me.Id : me.IsAdmin || HelperId is null || HelperId == me.Id;

    public void Close(DateTime now) => ClosedAt ??= now;

    private static string Clean(string? name) => (name ?? "").Trim();
}

/// <summary>Photos (or scanned copies) one helper took in one session.</summary>
public sealed record HelperTally(int SessionId, string Helper, int Count, DateTime LastAt);

/// <summary>One session on Martin's overview: how far, who took photos, when the latest came.</summary>
public sealed record SessionStatus(WorkSession Session, int Photos, int Scans, IReadOnlyList<string> Helpers, DateTime? LastAt)
{
    public static SessionStatus For(WorkSession s, IEnumerable<HelperTally> photos, IEnumerable<HelperTally> scans)
    {
        var p = photos.Where(t => t.SessionId == s.Id).ToList();
        var c = scans.Where(t => t.SessionId == s.Id).ToList();
        var last = p.Concat(c).Select(t => (DateTime?)t.LastAt).DefaultIfEmpty(null).Max();
        return new SessionStatus(s, p.Sum(t => t.Count), c.Sum(t => t.Count), p.Select(t => t.Helper).Distinct().Order().ToList(), last);
    }
}
