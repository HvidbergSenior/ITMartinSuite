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
    // Maps struck during this match's map-ban phase (ours and theirs).
    public HashSet<string> MapBans { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public int? ActivePlan { get; private set; }

    // Fixed prep-time budget per phase - Lobby and InGame are deliberately
    // absent (open-ended: "before anything starts" and "however long the
    // round takes" don't have a natural fixed duration). Advancing is always
    // a manual action; a phase's timer running out doesn't force anything -
    // it's a visible budget, not an auto-advance trigger.
    private static readonly Dictionary<MatchPhase, TimeSpan> PhaseDurations = new()
    {
        // Siege X (June 2025+) in-game timers as the team measured them: map ban 30 s,
        // operator bans are 15 s and happen before every round, operator
        // selection is 45 s. PostMatch is our own debrief window, not a game
        // timer.
        [MatchPhase.MapBans] = TimeSpan.FromSeconds(30),
        [MatchPhase.OperatorBans] = TimeSpan.FromSeconds(15),
        [MatchPhase.OperatorPick] = TimeSpan.FromSeconds(45),
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

    public IReadOnlyDictionary<string, string> MapPreferences => _team.MapPreferences;

    // Cycle none -> ban -> play -> none.
    public void CycleMapPreference(string map)
    {
        lock (_lock)
        {
            var cur = _team.MapPreferences.GetValueOrDefault(map);
            if (cur is null) _team.MapPreferences[map] = "ban";
            else if (cur == "ban") _team.MapPreferences[map] = "play";
            else _team.MapPreferences.Remove(map);
            SaveTeamSettings();
        }
        NotifyStateChanged();
    }

    public void ToggleMapBan(string map)
    {
        lock (_lock)
        {
            if (!MapBans.Remove(map)) MapBans.Add(map);
        }
        NotifyStateChanged();
    }

    public IReadOnlyList<MatchRecord> Matches => _team.Matches;

    public void LogMatch(MatchRecord m)
    {
        lock (_lock)
        {
            _team.Matches.Add(m);
            SaveTeamSettings();
        }
        NotifyStateChanged();
    }

    public void DeleteMatch(MatchRecord m)
    {
        lock (_lock)
        {
            _team.Matches.Remove(m);
            SaveTeamSettings();
        }
        NotifyStateChanged();
    }

    private static Domain.MapStats Aggregate(IEnumerable<MatchRecord> ms)
    {
        var l = ms.ToList();
        return l.Count == 0 ? Domain.MapStats.Empty : new Domain.MapStats(l.Count, l.Count(x => x.Won),
            l.Sum(x => x.AtkWon), l.Sum(x => x.AtkLost), l.Sum(x => x.DefWon), l.Sum(x => x.DefLost), l.Max(x => x.PlayedAtUtc));
    }

    public Domain.MapStats MapStats(string map) =>
        Aggregate(_team.Matches.Where(x => x.Map.Equals(map, StringComparison.OrdinalIgnoreCase)));

    public Domain.MapStats SiteStats(string map, string site) =>
        Aggregate(_team.Matches.Where(x => x.Map.Equals(map, StringComparison.OrdinalIgnoreCase) && x.Site.Equals(site, StringComparison.OrdinalIgnoreCase)));

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
        // After the match: "Afslut kamp" clears map, site, bans and side, so the next game starts clean.
        if (Phase == MatchPhase.PostMatch) { Reset(); return; }
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

    // Back one step (Lobby stays Lobby - going "back" from the lobby would
    // wrap to PostMatch, which is never what a mis-click meant).
    public void PreviousPhase()
    {
        var prev = Phase switch
        {
            MatchPhase.MapBans => MatchPhase.Lobby,
            MatchPhase.OperatorBans => MatchPhase.MapBans,
            MatchPhase.OperatorPick => MatchPhase.OperatorBans,
            MatchPhase.InGame => MatchPhase.OperatorPick,
            MatchPhase.PostMatch => MatchPhase.InGame,
            _ => MatchPhase.Lobby,
        };
        if (prev != Phase) SetPhase(prev);
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
            MapBans.Clear();
            ActivePlan = null;
            Phase = MatchPhase.Lobby;
            PhaseStartedAtUtc = DateTimeOffset.UtcNow;
        }
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnStateChanged?.Invoke();
}
