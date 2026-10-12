namespace ITMartinAdhd.Domain.Entities;

// Things not moved or saved again for 30 days are cleared away (with their photo) - the list stays about
// where things are now, not where they were once. Only applied when FindIt:ClearAfter30Days=true (off by default).
public static class KeepRule
{
    public static readonly TimeSpan KeepFor = TimeSpan.FromDays(30);

    public static DateTime Cutoff(DateTime now) => now - KeepFor;
    public static bool IsStale(StoredItem item, DateTime now) => item.UpdatedAt < Cutoff(now);
}
