namespace ITMartinR6Assistant.Domain;

// Team-wide standing config - anyone can change it, the change applies to
// everyone (live, via SessionStateService), and it survives a server
// restart (persisted as JSON in the Data folder, which is volume-mounted on
// the real deployment). Distinct from per-match state (Map/Site/Bans/Phase),
// which stays in-memory only and resets via SessionStateService.Reset().
public class TeamSettings
{
    public bool ShowBanners { get; set; } = true;
    public Dictionary<string, OperatorLoadout> DefaultLoadouts { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // Per-player personal loadout choices (player name -> operator -> their
    // version) - stored server-side, not localStorage, specifically so
    // teammates can see each other's choices on the overview page.
    public Dictionary<string, Dictionary<string, OperatorLoadout>> PlayerLoadouts { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // Latest pre-game system-check submission per player, for the same reason.
    public Dictionary<string, PlayerSetupRecord> PlayerSetups { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // Manual fallback values for fields the Specifikationer card couldn't
    // determine automatically (mouse/headset model, headset software) - see PlayerSpecs.
    public Dictionary<string, PlayerSpecs> PlayerSpecs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
