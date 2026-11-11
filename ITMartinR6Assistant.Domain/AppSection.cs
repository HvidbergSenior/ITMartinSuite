namespace ITMartinR6Assistant.Domain;

// What the OTHER players see (user 2026-11-11: "I will choose what can be seen by others", "frontpage simple overview for
// others", "statistikker mm should be clickable by me"). The owner (device unlocked with the PIN on /admin) always sees
// everything and switches each box (Fold Key) and menu page on/off for the others. Unknown keys are shown by default.
public static class AppSection
{
    // Menu pages
    public const string Maps = "maps";
    public const string Operators = "operatorer";
    public const string Weapons = "vaaben";

    // Kamp boxes that need the Fold key outside the Fold
    public const string Loadouts = "k-loadouts";
    public const string PcCheck = "k-pc";

    public static readonly (string Key, string Label)[] Pages =
    {
        (Maps, "Maps"),
        (Operators, "Operatører"),
        (Weapons, "Våben"),
    };

    // Simple front page for the others: map, map-ban, bans/good ops and sites. Statistics only when the owner shows them.
    public static readonly HashSet<string> HiddenByDefault = new(StringComparer.OrdinalIgnoreCase)
    {
        Operators, Weapons, Loadouts, PcCheck, "k-stats", "k-match", "k-send",
    };
}
