namespace ITMartinSalgsveje.Domain;

// Salgsveje (2026-11-04, copy of Club): every team but the public demo is a sales team - Salgsveje is the home page,
// the menu is Salgsveje / Opgaver / Steder og folk, no chat.
public static class ClubGroups
{
    public static bool IsBoard(string slug) => false;

    public static bool IsTasksOnly(string slug) => !slug.Equals("demo", StringComparison.OrdinalIgnoreCase);

    public static string HomePath(string slug) => IsTasksOnly(slug) ? $"/g/{slug}/salgsveje" : $"/g/{slug}";
}
