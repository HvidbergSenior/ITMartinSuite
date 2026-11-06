namespace ITMartinHusk.Domain;

// Mine job (user 2026-11-06: "in husk make a list of all my jobs I looked at") - after Claude suggested LOF Aarhus again although
// it was sent 23.10. One list of every job seen, applied for or left out, so nothing is suggested or sent twice, and the a-kasse's
// count (per week, per 4 weeks, advertised vs unsolicited, full-time vs part-time) can be seen at a glance. Only the ad's words -
// never the application text.

/// <summary>Seen = "Set" (to consider); Applied = "Søgt"; Skipped = "Fravalgt" (with the reason in the note).</summary>
public enum JobState { Seen, Applied, Skipped }

public sealed class JobLead
{
    public const int MaxTitle = 120, MaxText = 200, MaxUrl = 400;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public string Employer { get; set; } = "";
    public string Place { get; set; } = "";
    public string Url { get; set; } = "";
    /// <summary>True = an advertised job ("opslået"); false = unsolicited ("uopfordret").</summary>
    public bool Advertised { get; set; } = true;
    /// <summary>True = part-time ("deltid").</summary>
    public bool PartTime { get; set; }
    public JobState State { get; set; }
    public DateTime SeenAt { get; set; }
    public DateTime? AppliedAt { get; set; }
    public DateTime? Deadline { get; set; }
    public bool InJoblog { get; set; }
    public string Note { get; set; } = "";
    public DateTime UpdatedAt { get; set; }

    public static JobLead Create(string? title, string? employer, string? key, DateTime now)
    {
        var t = HuskRules.Clean(title, MaxTitle);
        if (t.Length == 0) throw new ArgumentException("Skriv, hvad jobbet hedder.");
        var e = HuskRules.Clean(employer, MaxTitle);
        var k = BallRules.Key(key);
        return new JobLead { Title = t, Employer = e, Key = k.Length > 0 ? k : BallRules.Key($"{e} {t}"), SeenAt = now, UpdatedAt = now };
    }

    public void Apply(DateTime day, DateTime now)
    {
        State = JobState.Applied;
        AppliedAt = day.Date;
        UpdatedAt = now;
    }

    public void Skip(string? why, DateTime now)
    {
        State = JobState.Skipped;
        AppliedAt = null;
        InJoblog = false;
        if (why is not null) Note = HuskRules.Clean(why, MaxText);
        UpdatedAt = now;
    }

    public void Reopen(DateTime now)
    {
        State = JobState.Seen;
        AppliedAt = null;
        InJoblog = false;
        UpdatedAt = now;
    }
}

/// <summary>The a-kasse's way of counting, over the applied jobs.</summary>
public sealed record JobCount(int ThisWeek, int Last4Weeks, int Advertised4, int PartTime4, int NotInJoblog);

public static class JobRules
{
    public static JobState? ParseState(string? s) => (s ?? "").Trim().ToLowerInvariant() switch
    {
        "set" or "seen" => JobState.Seen,
        "søgt" or "soegt" or "applied" => JobState.Applied,
        "fravalgt" or "skipped" => JobState.Skipped,
        _ => null,
    };

    public static string Name(JobState s) => s switch { JobState.Seen => "Set – overvej", JobState.Applied => "Søgt", _ => "Fravalgt" };

    /// <summary>Counts by applied day: this ISO week, and the last 4 weeks including this one (the a-kasse's rolling 4 weeks).</summary>
    public static JobCount Count(IEnumerable<JobLead> jobs, DateTime now)
    {
        var monday = now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7));
        var from4 = monday.AddDays(-21);
        var applied = jobs.Where(j => j.State == JobState.Applied && j.AppliedAt is not null).ToList();
        var four = applied.Where(j => j.AppliedAt >= from4 && j.AppliedAt < monday.AddDays(7)).ToList();
        return new JobCount(
            applied.Count(j => j.AppliedAt >= monday && j.AppliedAt < monday.AddDays(7)),
            four.Count, four.Count(j => j.Advertised), four.Count(j => j.PartTime),
            applied.Count(j => !j.InJoblog));
    }

    /// <summary>Was something already sent to this employer? (so the same place is not suggested twice).</summary>
    public static JobLead? AlreadyApplied(IEnumerable<JobLead> jobs, string? employer)
    {
        var e = BallRules.Key(employer);
        return e.Length == 0 ? null : jobs.FirstOrDefault(j => j.State == JobState.Applied && BallRules.Key(j.Employer) == e);
    }
}
