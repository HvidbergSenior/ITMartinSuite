using System.Text.Json;
using ITMartinR6Assistant.Domain;

namespace ITMartinR6Assistant.Infrastructure;

public class SessionStateService
{
    private readonly object _lock = new();
    private readonly string _teamSettingsPath = Path.Combine(AppContext.BaseDirectory, "Data", "team-settings.json");
    private TeamSettings _team;

    public SessionStateService()
    {
        _team = LoadTeamSettings();
    }

    private TeamSettings LoadTeamSettings()
    {
        try
        {
            if (File.Exists(_teamSettingsPath))
            {
                var json = File.ReadAllText(_teamSettingsPath);
                var loaded = JsonSerializer.Deserialize<TeamSettings>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (loaded is not null) return loaded;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // Fall through to defaults - a corrupt/unreadable file shouldn't crash startup.
        }
        return new TeamSettings();
    }

    private void SaveTeamSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_teamSettingsPath)!);
            File.WriteAllText(_teamSettingsPath, JsonSerializer.Serialize(_team, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (IOException)
        {
            // Best-effort persistence - a failed save just means this change
            // won't survive a restart, not a reason to break the live update.
        }
    }

    public string? Map { get; private set; }
    public string? Site { get; private set; }
    public string Side { get; private set; } = "Attack";
    public HashSet<string> Bans { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public int? ActivePlan { get; private set; }

    // Fixed prep-time budget per phase - Lobby and InGame are deliberately
    // absent (open-ended: "before anything starts" and "however long the
    // round takes" don't have a natural fixed duration). Advancing is always
    // a manual action; a phase's timer running out doesn't force anything -
    // it's a visible budget, not an auto-advance trigger.
    private static readonly Dictionary<MatchPhase, TimeSpan> PhaseDurations = new()
    {
        [MatchPhase.MapBans] = TimeSpan.FromMinutes(3),
        [MatchPhase.OperatorBans] = TimeSpan.FromMinutes(2),
        [MatchPhase.OperatorPick] = TimeSpan.FromMinutes(2),
        [MatchPhase.PostMatch] = TimeSpan.FromMinutes(5),
    };

    public MatchPhase Phase { get; private set; } = MatchPhase.Lobby;
    public DateTimeOffset PhaseStartedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
    public TimeSpan? PhaseDuration => PhaseDurations.GetValueOrDefault(Phase) is { } d && d > TimeSpan.Zero ? d : null;

    // Team-wide standing config - anyone can change it, the change applies
    // live to everyone connected, and it's persisted to disk so it survives
    // a server restart (see TeamSettings). Not cleared by Reset(): this is
    // standing config, not per-match state. Per-player loadout overrides
    // live client-side (localStorage) instead - see Loadouts.razor.
    public bool ShowBanners => _team.ShowBanners;
    public IReadOnlyDictionary<string, OperatorLoadout> DefaultLoadouts => _team.DefaultLoadouts;

    public OperatorLoadout GetDefaultLoadout(string operatorName) =>
        _team.DefaultLoadouts.TryGetValue(operatorName, out var loadout) ? loadout : new OperatorLoadout();

    public void SetDefaultLoadout(string operatorName, OperatorLoadout loadout)
    {
        lock (_lock)
        {
            _team.DefaultLoadouts[operatorName] = loadout;
            SaveTeamSettings();
        }
        NotifyStateChanged();
    }

    public void SetShowBanners(bool show)
    {
        lock (_lock)
        {
            _team.ShowBanners = show;
            SaveTeamSettings();
        }
        NotifyStateChanged();
    }

    // Personal loadout choices per player - stored server-side (not
    // localStorage) so teammates can see each other's choices for
    // comparison, same as the always-visible "Foretrukket" default.
    public OperatorLoadout? GetPlayerLoadout(string player, string operatorName) =>
        _team.PlayerLoadouts.TryGetValue(player, out var byOp) && byOp.TryGetValue(operatorName, out var loadout)
            ? loadout : null;

    public IReadOnlyDictionary<string, OperatorLoadout> GetPlayerLoadouts(string player) =>
        _team.PlayerLoadouts.TryGetValue(player, out var byOp) ? byOp : new Dictionary<string, OperatorLoadout>();

    public void SetPlayerLoadout(string player, string operatorName, OperatorLoadout? loadout)
    {
        lock (_lock)
        {
            if (!_team.PlayerLoadouts.TryGetValue(player, out var byOp))
            {
                if (loadout is null) return;
                byOp = new Dictionary<string, OperatorLoadout>(StringComparer.OrdinalIgnoreCase);
                _team.PlayerLoadouts[player] = byOp;
            }
            if (loadout is null) byOp.Remove(operatorName); else byOp[operatorName] = loadout;
            SaveTeamSettings();
        }
        NotifyStateChanged();
    }

    // Latest pre-game system-check submission per player - for the team
    // overview page, so a teammate's setup can be looked at when helping
    // troubleshoot ("why can't X hear anyone").
    public IReadOnlyDictionary<string, PlayerSetupRecord> PlayerSetups => _team.PlayerSetups;

    public void SetPlayerSetup(string player, PlayerSetupRecord record)
    {
        lock (_lock)
        {
            _team.PlayerSetups[player] = record;
            SaveTeamSettings();
        }
        NotifyStateChanged();
    }

    public IReadOnlyCollection<string> KnownPlayers
    {
        get
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            names.UnionWith(_team.PlayerLoadouts.Keys);
            names.UnionWith(_team.PlayerSetups.Keys);
            names.UnionWith(_team.PlayerSpecs.Keys);
            return names;
        }
    }

    // Manual fallback values for the Specifikationer card - only the fields
    // a player typed in by hand because auto-detect (PreGameCheck.ps1) had
    // nothing for them.
    public PlayerSpecs GetPlayerSpecs(string player) =>
        _team.PlayerSpecs.TryGetValue(player, out var specs) ? specs : new PlayerSpecs();

    public void SetPlayerSpecs(string player, PlayerSpecs specs)
    {
        lock (_lock)
        {
            _team.PlayerSpecs[player] = specs;
            SaveTeamSettings();
        }
        NotifyStateChanged();
    }

    public event Action? OnStateChanged;

    public void AdvancePhase()
    {
        var next = Phase switch
        {
            MatchPhase.Lobby => MatchPhase.MapBans,
            MatchPhase.MapBans => MatchPhase.OperatorBans,
            MatchPhase.OperatorBans => MatchPhase.OperatorPick,
            MatchPhase.OperatorPick => MatchPhase.InGame,
            MatchPhase.InGame => MatchPhase.PostMatch,
            MatchPhase.PostMatch => MatchPhase.Lobby,
            _ => MatchPhase.Lobby,
        };
        SetPhase(next);
    }

    public void SetPhase(MatchPhase phase)
    {
        lock (_lock)
        {
            Phase = phase;
            PhaseStartedAtUtc = DateTimeOffset.UtcNow;
        }
        NotifyStateChanged();
    }

    public void SetMap(string map)
    {
        lock (_lock)
        {
            Map = map;
            Site = null;
            ActivePlan = null;
        }
        NotifyStateChanged();
    }

    public void SetSite(string site)
    {
        lock (_lock)
        {
            Site = site;
            ActivePlan = null;
        }
        NotifyStateChanged();
    }

    public void SetSide(string side)
    {
        lock (_lock) { Side = side; }
        NotifyStateChanged();
    }

    public void ToggleBan(string operatorName)
    {
        lock (_lock)
        {
            if (!Bans.Remove(operatorName))
                Bans.Add(operatorName);
        }
        NotifyStateChanged();
    }

    public void SetActivePlan(int? planNumber)
    {
        lock (_lock) { ActivePlan = planNumber; }
        NotifyStateChanged();
    }

    public void Reset()
    {
        lock (_lock)
        {
            Map = null;
            Site = null;
            Side = "Attack";
            Bans.Clear();
            ActivePlan = null;
            Phase = MatchPhase.Lobby;
            PhaseStartedAtUtc = DateTimeOffset.UtcNow;
        }
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnStateChanged?.Invoke();
}
