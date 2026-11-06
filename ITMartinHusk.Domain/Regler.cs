using System.Globalization;

namespace ITMartinHusk.Domain;

// Regler (user 2026-11-06, after Mit AKA said "ingen jobsøgninger uge 44"): "on husk the rules should be shown. Understandable".
// Plain words, what to do, and where the rule comes from. Checked 2026-11-06: the law sets NO fixed number of applications per
// week - the a-kasse sets it in the person's own "Krav til jobsøgning" - so the number is the person's own, typed in once.
// Help and fix, no fingers pointed ([[feedback-help-and-fix-not-blame]]).

/// <summary>One rule: the short rule, why it matters, what you do, and the source.</summary>
public sealed record HuskRule(string Title, string Plain, string WhatToDo, string Source, string Url);

public static class RuleBook
{
    public const string Joblog = "https://jobnet.dk";   // Mit Jobnet -> Joblog (log in first)

    /// <summary>Data, not code: an a-kasse or kommune that offers Husk can change the texts.</summary>
    public static readonly IReadOnlyList<HuskRule> Dagpenge =
    [
        new("Søg job hver uge – og skriv det i Joblog",
            "Når du får dagpenge, skal du løbende søge job og skrive din jobsøgning ind i Joblog på Jobnet. Hvor mange job, bestemmer din a-kasse – det står i dit eget \"Krav til jobsøgning\". Der er ikke ét tal, der gælder for alle.",
            "Find dit tal i brevet \"Krav til jobsøgning\" fra a-kassen, og skriv det her på siden. Så minder Husk dig hver fredag.",
            "Bekendtgørelse om rådighed", "https://www.retsinformation.dk/eli/lta/2026/46"),
        new("For lidt jobsøgning kan stoppe dagpengene",
            "A-kassen kigger i din Joblog. Mangler der jobsøgning, kan de vurdere, at du ikke står til rådighed – og så må de ikke udbetale dagpenge. Ofte får du først en frist til at skrive det ind.",
            "Mangler en uge? Skriv jobbet ind i Joblog nu – når du tilføjer et job, kan du vælge datoen, så det lander i den rigtige uge. Søgte du ikke den uge, så skriv til a-kassen og forklar hvorfor, samme dag.",
            "Ase: Krav til din jobsøgning", "https://www.ase.dk/faa-svar/dagpenge/krav-til-din-jobsoegning"),
        new("Mød op til dine møder",
            "Møder i Jobnet og hos a-kassen er en del af dagpengene. Kommer du ikke, kan du miste dagpenge – fra mødet, og indtil du kontakter dem.",
            "Nåede du ikke et møde? Skriv eller ring samme dag. Så mister du kun den ene dag. Husk spørger dig et kvarter efter mødet.",
            "borger.dk: Arbejdsløshedsdagpenge", "https://www.borger.dk/arbejde-dagpenge-ferie/arbejdsloeshed/arbejdsloeshedsdagpenge"),
        new("Indkaldelser kommer i Jobnet og Digital Post",
            "Møder og breve kommer i Jobnet, Mit AKA og Digital Post. Du får ikke altid en sms eller mail om det – og indkaldelsen gælder alligevel.",
            "Tag tjek-runden i Husk én gang om dagen: Jobnet, Mit AKA og Digital Post, ét sted ad gangen.",
            "borger.dk: Arbejdsløshedsdagpenge", "https://www.borger.dk/arbejde-dagpenge-ferie/arbejdsloeshed/arbejdsloeshedsdagpenge"),
    ];

    public const string Disclaimer = "Reglerne er forklaret med almindelige ord. Det, der står i dine egne breve fra a-kassen og kommunen, gælder altid.";

    /// <summary>The week a job-search reminder is about: from Friday it is this week; Monday-Thursday it is last week
    /// (so a week is never forgotten just because Sunday passed).</summary>
    public static DateTime JobWeekMonday(DateTime now)
    {
        var monday = now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7));
        return now.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday or DayOfWeek.Sunday ? monday : monday.AddDays(-7);
    }

    public static string WeekKey(DateTime monday) =>
        $"{ISOWeek.GetYear(monday)}-W{ISOWeek.GetWeekOfYear(monday):00}";

    public static int WeekNumber(DateTime monday) => ISOWeek.GetWeekOfYear(monday);

    /// <summary>Shown until the person presses "Det er skrevet ind" for that week. Off when no number is set.</summary>
    public static bool JobLogDue(int perWeek, ICollection<string> doneWeeks, DateTime now) =>
        perWeek > 0 && !doneWeeks.Contains(WeekKey(JobWeekMonday(now)));

    /// <summary>The weekly push: Friday from 12 ("skriv ugens jobsøgning ind"), and Monday from 9 if that week is still missing.
    /// Once each (sent-log key), never at night.</summary>
    public static List<(string Key, PushMessage Message)> JobLogPushes(int perWeek, ICollection<string> doneWeeks, DateTime now, ICollection<string> sent)
    {
        var list = new List<(string, PushMessage)>();
        if (!JobLogDue(perWeek, doneWeeks, now) || now.Hour >= 21) return list;
        var monday = JobWeekMonday(now);
        var week = WeekKey(monday);
        var n = perWeek == 1 ? "1 job" : $"{perWeek} job";
        if (now.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday or DayOfWeek.Sunday && now >= monday.AddDays(4).AddHours(12))
            Add($"joblog|{week}|fre", new PushMessage($"Ugens jobsøgning – uge {WeekNumber(monday)}",
                $"Dit krav er {n} om ugen. Skriv dem i Joblog på Jobnet, og tryk \"Det er skrevet ind\" i Husk."));
        else if (now.DayOfWeek is not (DayOfWeek.Friday or DayOfWeek.Saturday or DayOfWeek.Sunday) && now >= monday.AddDays(7).AddHours(9))
            Add($"joblog|{week}|man", new PushMessage($"Uge {WeekNumber(monday)} mangler i Joblog",
                "Skriv jobbene ind nu – du kan vælge datoen, når du tilføjer et job. Søgte du ikke, så skriv til a-kassen i dag."));
        return list;

        void Add(string key, PushMessage msg)
        {
            if (!sent.Contains(key)) list.Add((key, msg));
        }
    }
}
