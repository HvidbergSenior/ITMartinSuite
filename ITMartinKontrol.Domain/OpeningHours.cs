namespace ITMartinKontrol.Domain;

// Opening hours per app (user 2026-10-19: "polstrer should only be on 7-17 o clock"). Kontrol starts the app when
// its hours begin and stops it when they end - only at those two moments, so a start or stop by hand in between is
// left alone. Hours are whole hours, Danish time; "22-06" runs over midnight.
public sealed record OpeningHours(int From, int To)
{
    /// <summary>The choices in Kontrol's list (besides "Altid").</summary>
    public static readonly string[] Choices = ["07-17", "08-16", "08-20", "06-22"];

    /// <summary>"07-17" -> 7..17; null for "altid", empty or nonsense.</summary>
    public static OpeningHours? Parse(string? text)
    {
        var parts = (text ?? "").Split('-', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !int.TryParse(parts[0], out var from) || !int.TryParse(parts[1], out var to)) return null;
        if (from is < 0 or > 23 || to is < 0 or > 24 || from == to % 24) return null;
        return new OpeningHours(from, to % 24);
    }

    public bool IsOpenAt(TimeOnly t) =>
        From < To ? t.Hour >= From && t.Hour < To
                  : t.Hour >= From || t.Hour < To;

    public override string ToString() => $"{From:00}-{To:00}";
}

public enum HoursStep { None, Start, Stop }

public static class HoursPolicy
{
    /// <summary>What to do with an app that has opening hours. wasOpen = the state at the last check; null = Kontrol has
    /// just started and does not know - then it only closes an app running outside its hours, it never starts one.</summary>
    public static HoursStep Decide(bool? wasOpen, bool isOpen, bool running) => wasOpen switch
    {
        null => !isOpen && running ? HoursStep.Stop : HoursStep.None,
        _ when wasOpen == isOpen => HoursStep.None,
        _ => isOpen ? (running ? HoursStep.None : HoursStep.Start) : (running ? HoursStep.Stop : HoursStep.None),
    };
}
