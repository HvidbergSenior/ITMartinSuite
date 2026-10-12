namespace ITMartinClub.Server.Services;

// Until step 3 (module switches per team): which groups are a "board/association" Club - Beskeder is the home page,
// the menu is Beskeder / Kalender / Medlemmer, members log in by invitation link (no PIN).
public static class ClubGroups
{
    private static readonly HashSet<string> MessagesFirst = new(StringComparer.OrdinalIgnoreCase) { "skelagerhoejen" };

    // Teams that only want tasks and addresses (Bogshoppen 2026-10-06: "ONLY tasks and adresses") - Opgaver is the home page,
    // the menu is Opgaver / Adresser, no chat.
    private static readonly HashSet<string> TasksAndAddresses = new(StringComparer.OrdinalIgnoreCase) { "bogshoppen" };

    public static bool IsBoard(string slug) => MessagesFirst.Contains(slug);

    public static bool IsTasksOnly(string slug) => TasksAndAddresses.Contains(slug);

    public static string HomePath(string slug) =>
        IsBoard(slug) ? $"/g/{slug}/beskeder" : IsTasksOnly(slug) ? $"/g/{slug}/opgaver" : $"/g/{slug}";
}
