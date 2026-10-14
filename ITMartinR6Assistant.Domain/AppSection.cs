namespace ITMartinR6Assistant.Domain;

// The app shows only maps + statistics by default (user 2026-10-14); Admin can switch these extra sections on for the team.
public static class AppSection
{
    public const string Operators = "operatorer";
    public const string Weapons = "vaaben";
    public const string Loadouts = "loadouts";
    public const string PcCheck = "pc";

    public static readonly (string Key, string Label)[] Extras =
    {
        (Operators, "Operatører"),
        (Weapons, "Våben"),
        (Loadouts, "Loadouts"),
        (PcCheck, "PC-tjek og Siege-indstillinger"),
    };
}
