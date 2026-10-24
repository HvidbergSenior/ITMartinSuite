namespace ITMartinPoll.Domain;

// GDPR in Stem (user 2026-10-24): a poll keeps who answered while it is in use. 6 months after it closed and went quiet,
// names become "Deltager 1, 2 ..." (results and comments stay readable, also Martin's own old votes like the logo vote),
// e-mails and phone numbers are emptied and chat messages deleted. Same numbers on itmartin.dk/vilkaar.
public static class PrivacyRules
{
    public const int KeepPersonalMonths = 6;
    private const string Prefix = "Deltager ";

    /// <param name="deadline">As stored (local time), like Poll.HasDeadlinePassed reads it.</param>
    public static bool ShouldForget(bool isActive, DateTime? deadline, DateTime lastActivityUtc, DateTime nowUtc) =>
        (!isActive || (deadline is { } d && d.ToUniversalTime() < nowUtc)) && lastActivityUtc <= nowUtc.AddMonths(-KeepPersonalMonths);

    public static bool IsPseudonym(string name) => name.StartsWith(Prefix) && int.TryParse(name[Prefix.Length..], out _);

    /// <summary>Real name -> "Deltager n", the same person always the same number, numbers after the ones already given.</summary>
    public static Dictionary<string, string> Pseudonyms(IEnumerable<string> namesInOrder)
    {
        var names = namesInOrder.ToList();
        var next = names.Where(IsPseudonym).Select(n => int.Parse(n[Prefix.Length..])).DefaultIfEmpty(0).Max();
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in names.Where(n => n.Trim() != "" && !IsPseudonym(n)))
            if (!map.ContainsKey(n)) map[n] = Prefix + ++next;
        return map;
    }
}
