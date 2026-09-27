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

    // A match is several rounds; op-ban -> op-pick -> in-game repeats every round.
    public int Round { get; private set; } = 1;
    private readonly List<RoundResult> _rounds = new();
    public IReadOnlyList<RoundResult> Rounds => _rounds;
    public int RoundsWon => _rounds.Count(r => r.Won);
    public int RoundsLost => _rounds.Count(r => !r.Won);

    // The operators in this round: who we play (max 5) and who the opponents play (seen in the round).
    // The in-round screens show only these, so the team reads about the fight they are actually in.
    public HashSet<string> OurOps { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> TheirOps { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void ToggleOurOp(string name)
    {
        lock (_lock) { if (!OurOps.Remove(name) && OurOps.Count < 5) OurOps.Add(name); }
        NotifyStateChanged();
    }

    public void ToggleTheirOp(string name)
    {
        lock (_lock) { if (!TheirOps.Remove(name) && TheirOps.Count < 5) TheirOps.Add(name); }
        NotifyStateChanged();
    }

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
            // Its rounds now live in the log - drop the live copy so they are not counted twice.
            if (m.Rounds.Count > 0) _rounds.Clear();
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

    // Per-site round record (user: "pct win/lose on each map and on each site"): every round tapped won/lost is
    // stored with its site, from logged matches plus the match running right now. Played/Won = rounds.
    private IEnumerable<RoundResult> RoundsOn(string map)
    {
        var logged = _team.Matches.Where(x => x.Map.Equals(map, StringComparison.OrdinalIgnoreCase)).SelectMany(x => x.Rounds);
        return Map is not null && Map.Equals(map, StringComparison.OrdinalIgnoreCase) ? logged.Concat(_rounds) : logged;
    }

    public Domain.MapStats SiteRounds(string map, string site)
    {
        var r = RoundsOn(map).Where(x => site.Equals(x.Site, StringComparison.OrdinalIgnoreCase)).ToList();
        return r.Count == 0 ? Domain.MapStats.Empty : new Domain.MapStats(r.Count, r.Count(x => x.Won),
            r.Count(x => x.Side == "Attack" && x.Won), r.Count(x => x.Side == "Attack" && !x.Won),
            r.Count(x => x.Side != "Attack" && x.Won), r.Count(x => x.Side != "Attack" && !x.Won), null);
    }

    // "Start med dette site": the site where we win the most rounds on this side (needs 2+ rounds there).
    public (string Site, int Pct, int Rounds)? BestSite(string map, IEnumerable<string> sites, string side)
    {
        var atk = side == "Attack";
        return sites.Select(s => (s, st: SiteRounds(map, s)))
            .Select(x => (x.s, won: atk ? x.st.AtkWon : x.st.DefWon, n: atk ? x.st.AtkWon + x.st.AtkLost : x.st.DefWon + x.st.DefLost))
            .Where(x => x.n >= 2)
            .OrderByDescending(x => (double)x.won / x.n).ThenByDescending(x => x.n)
            .Select(x => ((string Site, int Pct, int Rounds)?)(x.s, 100 * x.won / x.n, x.n))
            .FirstOrDefault();
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

    // Coach mode: one device (PIN-claimed, see CoachService) runs the phase screen - phase, map, site, side, bans,
    // ops, and which cards are open. Everyone else follows read-only. No coach = everyone can steer, as before.
    public string? CoachId { get; private set; }
    public string? CoachName { get; private set; }
    public string CoachCall { get; private set; } = "";
    private readonly Dictionary<string, bool> _shown = new(StringComparer.OrdinalIgnoreCase);
    // Op-ban site cards the coach has opened ("map|site").
    public HashSet<string> OpenSites { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void SetCoach(string? id, string? name)
    {
        lock (_lock) { CoachId = id; CoachName = id is null ? null : name; }
        NotifyStateChanged();
    }

    public void SetCoachCall(string text)
    {
        lock (_lock) { CoachCall = text.Trim(); }
        NotifyStateChanged();
    }

    // Card open/closed as the coach left it, per phase (so each phase keeps its own defaults).
    public bool? GetShown(string key) { lock (_lock) { return _shown.TryGetValue(Phase + ":" + key, out var v) ? v : null; } }

    public void SetShown(string key, bool open)
    {
        lock (_lock) { _shown[Phase + ":" + key] = open; }
        NotifyStateChanged();
    }

    public void ToggleOpenSite(string key)
    {
        lock (_lock) { if (!OpenSites.Remove(key)) OpenSites.Add(key); }
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

    // Round over: remember it, then the next round starts at operator bans with a fresh site and bans.
    public void EndRound(bool won)
    {
        lock (_lock)
        {
            _rounds.Add(new RoundResult(Round, Side, Site, won, OurOps.ToList(), TheirOps.ToList()));
            Round++;
            Bans.Clear();
            OurOps.Clear();
            TheirOps.Clear();
            Site = null;
            ActivePlan = null;
            Phase = MatchPhase.OperatorBans;
            PhaseStartedAtUtc = DateTimeOffset.UtcNow;
        }
        NotifyStateChanged();
    }

    // Mis-tap on "Runde vundet/tabt": take the last round back.
    public void UndoLastRound()
    {
        lock (_lock)
        {
            if (_rounds.Count == 0) return;
            var last = _rounds[^1];
            _rounds.RemoveAt(_rounds.Count - 1);
            Round = last.Number;
            Side = last.Side;
            Site = last.Site;
            OurOps.Clear(); OurOps.UnionWith(last.OurOps);
            TheirOps.Clear(); TheirOps.UnionWith(last.TheirOps);
            Phase = MatchPhase.InGame;
            PhaseStartedAtUtc = DateTimeOffset.UtcNow;
        }
        NotifyStateChanged();
    }

    public void EndMatch() => SetPhase(MatchPhase.PostMatch);

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
        // Switching side means other operators on both teams.
        lock (_lock) { if (Side != side) { OurOps.Clear(); TheirOps.Clear(); } Side = side; }
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
            _rounds.Clear();
            Round = 1;
            OurOps.Clear();
            TheirOps.Clear();
            OpenSites.Clear();
            CoachCall = "";
            Phase = MatchPhase.Lobby;
            PhaseStartedAtUtc = DateTimeOffset.UtcNow;
        }
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnStateChanged?.Invoke();
}
