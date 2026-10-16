namespace ITMartinKontrol.Domain;

// Martin's priority per container (2026-10-16, after upload-web's restart loop hung the NAS):
// 1 = Skal køre (never stopped automatically), 2 = Vigtig (only when memory is critical),
// 3 = Kan vente (stopped first). Unset = Vigtig, so nothing is stopped until he has chosen.
public static class Priority
{
    public const int MustRun = 1, Important = 2, CanWait = 3;
    public static bool Valid(int p) => p is MustRun or Important or CanWait;
    public static string Label(int p) => p switch { MustRun => "Skal køre", CanWait => "Kan vente", _ => "Vigtig" };
}

public enum Pressure { Ok, Low, Critical }

public sealed record GuardLimits(int LowMb = 250, int CriticalMb = 120, int SwapInPerSec = 400);

public sealed record GuardCandidate(string Name, int Priority, double MemMb, bool Protected);

public static class GuardPolicy
{
    /// <summary>Low = little free RAM or the host is paging in hard (that is what hung the NAS); critical = almost none left.</summary>
    public static Pressure Assess(double availMb, double swapInPerSec, GuardLimits l) =>
        availMb < l.CriticalMb ? Pressure.Critical
        : availMb < l.LowMb || swapInPerSec > l.SwapInPerSec ? Pressure.Low
        : Pressure.Ok;

    /// <summary>The app to stop now, or null: "Kan vente" first, "Vigtig" only when critical, biggest first. Never "Skal køre" or protected.</summary>
    public static string? PickVictim(IEnumerable<GuardCandidate> running, Pressure p) =>
        p == Pressure.Ok ? null
        : running.Where(c => !c.Protected && (c.Priority == Priority.CanWait || (p == Pressure.Critical && c.Priority == Priority.Important)))
            .OrderByDescending(c => c.Priority).ThenByDescending(c => c.MemMb).ThenBy(c => c.Name, StringComparer.Ordinal)
            .FirstOrDefault()?.Name;
}

/// <summary>
/// "Genstart i rækkefølge": stop the apps, then start them one by one - Skal køre, then Vigtig, then
/// Kan vente - so the host never starts ten .NET apps at once (that is what a reboot does).
/// </summary>
public static class RestartPlan
{
    /// <summary>Order to start in. Protected containers are never part of the plan (they keep running).</summary>
    public static List<string> StartOrder(IEnumerable<GuardCandidate> apps) =>
        apps.Where(a => !a.Protected)
            .OrderBy(a => a.Priority).ThenBy(a => a.Name, StringComparer.Ordinal)
            .Select(a => a.Name).ToList();

    /// <summary>Start the next one only with room to spare; "Kan vente" apps are skipped while memory is low.</summary>
    public static bool MayStart(int priority, double availMb, GuardLimits l) =>
        priority == Priority.MustRun || availMb >= l.LowMb + 100;
}
