using ITMartinStarRealms.Server.Data;
using ITMartinStarRealms.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinStarRealms.Server.Services;

// No SignalR/hub here on purpose - the Cloudflare Tunnel this app is served through
// kills long-lived SignalR connections, so cross-device sync is done via REST + JS
// polling instead (see Program.cs's /api/sessions endpoints).
public sealed class GameService(StarRealmsDbContext db)
{
    public const int MaxPlayers = 6;

    // Serializes point adjustments per session so two rapid taps (e.g. a
    // double-tap on +1) can't race: each request loads the player row, adds
    // its delta, and saves in three separate steps, so without this lock two
    // overlapping requests can both read the same "before" value and one
    // update gets silently lost.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> SessionLocks = new();

    private static SemaphoreSlim GetSessionLock(string code) =>
        SessionLocks.GetOrAdd(code.ToUpper(), _ => new SemaphoreSlim(1, 1));

    private static readonly string[] Colors =
    [
        "#e74c3c", "#3498db", "#2ecc71", "#f1c40f", "#9b59b6", "#e67e22"
    ];

    public static readonly string[] AvailableColors = Colors;

    public Task<List<GameRuleset>> GetRulesetsAsync() =>
        db.Rulesets.OrderByDescending(r => r.IsBuiltIn).ThenBy(r => r.Name).ToListAsync();

    public async Task<GameRuleset> CreateCustomRulesetAsync(
        string name, string description, int minPlayers, int maxPlayers,
        bool isTeamMode, int playersPerTeam, bool sharedTeamPool, int startingPoints, string createdByName)
    {
        var ruleset = new GameRuleset
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Mit eget regelsæt" : name.Trim(),
            Description = description.Trim(),
            MinPlayers = Math.Clamp(minPlayers, 1, MaxPlayers),
            MaxPlayers = Math.Clamp(maxPlayers, minPlayers, MaxPlayers),
            IsTeamMode = isTeamMode,
            PlayersPerTeam = isTeamMode ? Math.Max(1, playersPerTeam) : 0,
            SharedTeamPool = isTeamMode && sharedTeamPool,
            DefaultStartingPoints = startingPoints > 0 ? Math.Clamp(startingPoints, 50, 100) : 50,
            IsBuiltIn = false,
            CreatedByProfileName = createdByName
        };
        db.Rulesets.Add(ruleset);
        await db.SaveChangesAsync();
        return ruleset;
    }

    public Task<PlayerProfile?> FindProfileAsync(string deviceToken) =>
        db.Profiles.FirstOrDefaultAsync(p => p.DeviceToken == deviceToken);

    public Task<PlayerProfile?> FindProfileByIdAsync(Guid id) =>
        db.Profiles.FirstOrDefaultAsync(p => p.Id == id);

    // Only ~20-40 real players total - cheap to return in full for a name
    // picker rather than building search/autocomplete.
    public Task<List<PlayerProfile>> GetAllProfilesAsync() =>
        db.Profiles.OrderBy(p => p.Name).ToListAsync();

    // Only drops the profile row itself (the name picker entry) - past
    // GameResultPlayer rows keep their own copy of Name/ProfileId, so
    // historical stats/leaderboard entries for this person are untouched.
    public async Task DeleteProfileAsync(Guid id)
    {
        var profile = await db.Profiles.FindAsync(id);
        if (profile is null) return;
        db.Profiles.Remove(profile);
        await db.SaveChangesAsync();
    }

    private static string HashPin(string pin) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(pin)));

    public async Task<PlayerProfile> GetOrCreateProfileAsync(string deviceToken, string name, string avatar, string? pin = null)
    {
        var existing = await db.Profiles.FirstOrDefaultAsync(p => p.DeviceToken == deviceToken);
        if (existing is not null)
        {
            // Already this device's own profile - no PIN needed to keep using
            // it. But renaming it onto a DIFFERENT existing profile's exact
            // name still has to go through the same uniqueness/PIN check as
            // claiming an identity below - otherwise the profile editor was a
            // backdoor around the PIN (and around the anti-splinter design)
            // just by typing someone else's name into your own profile.
            if (!string.IsNullOrWhiteSpace(name))
            {
                var trimmed = name.Trim();
                if (!string.Equals(trimmed, existing.Name, StringComparison.OrdinalIgnoreCase))
                {
                    var collision = await db.Profiles.FirstOrDefaultAsync(
                        p => p.Id != existing.Id && p.Name.ToLower() == trimmed.ToLower());
                    if (collision is not null)
                    {
                        if (collision.PinHash is not null &&
                            (string.IsNullOrEmpty(pin) || HashPin(pin) != collision.PinHash))
                            throw new InvalidOperationException("Forkert eller manglende PIN-kode for dette navn.");

                        collision.DeviceToken = deviceToken;
                        if (!string.IsNullOrWhiteSpace(avatar)) collision.Avatar = avatar;
                        await db.SaveChangesAsync();
                        return collision;
                    }
                    existing.Name = trimmed;
                }
            }
            if (!string.IsNullOrWhiteSpace(avatar)) existing.Avatar = avatar;
            await db.SaveChangesAsync();
            return existing;
        }

        // Names are meant to be unique per person - if this exact name already
        // has a profile (picked from the list, or typed fresh on a new/cleared
        // device), reuse that identity and its win/loss history instead of
        // splintering it into a second profile with the same name. If that
        // identity is PIN-protected, a different device needs the PIN to play
        // as them - viewing their stats never needs it, only this path does.
        if (!string.IsNullOrWhiteSpace(name))
        {
            var trimmed = name.Trim();
            var byName = await db.Profiles.FirstOrDefaultAsync(p => p.Name.ToLower() == trimmed.ToLower());
            if (byName is not null)
            {
                if (byName.PinHash is not null &&
                    (string.IsNullOrEmpty(pin) || HashPin(pin) != byName.PinHash))
                    throw new InvalidOperationException("Forkert eller manglende PIN-kode for dette navn.");

                byName.DeviceToken = deviceToken;
                if (!string.IsNullOrWhiteSpace(avatar)) byName.Avatar = avatar;
                await db.SaveChangesAsync();
                return byName;
            }
        }

        var profile = new PlayerProfile
        {
            DeviceToken = deviceToken,
            Name = string.IsNullOrWhiteSpace(name) ? "Spiller" : name.Trim(),
            Avatar = string.IsNullOrWhiteSpace(avatar) ? "🚀" : avatar,
            PinHash = string.IsNullOrWhiteSpace(pin) ? null : HashPin(pin)
        };
        db.Profiles.Add(profile);
        await db.SaveChangesAsync();
        return profile;
    }

    public async Task<GameSession> CreateAsync(Guid rulesetId, int startingPoints, bool isRanked = true)
    {
        var ruleset = await db.Rulesets.FindAsync(rulesetId)
            ?? throw new InvalidOperationException("Regelsæt ikke fundet");

        var session = new GameSession
        {
            Code = GenerateCode(),
            StartingPoints = startingPoints > 0 ? Math.Clamp(startingPoints, 50, 100) : ruleset.DefaultStartingPoints,
            RulesetId = ruleset.Id,
            RulesetName = ruleset.Name,
            IsTeamMode = ruleset.IsTeamMode,
            SharedTeamPool = ruleset.SharedTeamPool,
            IsRanked = isRanked
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    public Task<GameSession?> GetByCodeAsync(string code) =>
        db.Sessions
            .Include(s => s.Players)
            .FirstOrDefaultAsync(s => s.Code == code.ToUpper());

    public async Task<GamePlayer> GetOrCreatePlayerAsync(string code, string token, string name, Guid? profileId, string avatar, string? color = null)
    {
        var session = await db.Sessions
            .Include(s => s.Players)
            .FirstOrDefaultAsync(s => s.Code == code.ToUpper())
            ?? throw new InvalidOperationException("Spil ikke fundet");

        var existing = session.Players.FirstOrDefault(p => p.Token == token);
        if (existing is not null) return existing;

        if (session.Players.Count >= MaxPlayers)
            throw new InvalidOperationException("Spillet er fuldt (maks 6 spillere)");

        var ruleset = await db.Rulesets.FindAsync(session.RulesetId);
        int? team = null;
        if (session.IsTeamMode && ruleset is { PlayersPerTeam: > 0 })
            team = session.Players.Count / ruleset.PlayersPerTeam;

        var takenColors = session.Players.Select(p => p.Color).ToHashSet();
        var resolvedColor = !string.IsNullOrWhiteSpace(color) && !takenColors.Contains(color)
            ? color
            : Colors.FirstOrDefault(c => !takenColors.Contains(c)) ?? Colors[session.Players.Count % Colors.Length];

        var player = new GamePlayer
        {
            SessionId  = session.Id,
            Token      = token,
            ProfileId  = profileId,
            Name       = string.IsNullOrWhiteSpace(name) ? $"Spiller {session.Players.Count + 1}" : name.Trim(),
            Avatar     = string.IsNullOrWhiteSpace(avatar) ? "🚀" : avatar,
            Color      = resolvedColor,
            Points     = session.StartingPoints,
            Team       = team,
            SortOrder  = session.Players.Count
        };
        db.Players.Add(player);

        await db.SaveChangesAsync();
        return player;
    }

    public async Task StartAsync(string code)
    {
        var session = await db.Sessions
            .FirstOrDefaultAsync(s => s.Code == code.ToUpper())
            ?? throw new InvalidOperationException("Spil ikke fundet");

        session.HasStarted = true;
        await db.SaveChangesAsync();
    }

    public async Task AdjustPointsAsync(string code, Guid playerId, int delta)
    {
        var sessionLock = GetSessionLock(code);
        await sessionLock.WaitAsync();
        try
        {
            var session = await db.Sessions
                .Include(s => s.Players)
                .FirstOrDefaultAsync(s => s.Code == code.ToUpper())
                ?? throw new InvalidOperationException("Spil ikke fundet");

            if (!session.HasStarted)
                throw new InvalidOperationException("Spillet er ikke startet endnu");

            var player = session.Players.FirstOrDefault(p => p.Id == playerId)
                ?? throw new InvalidOperationException("Spiller ikke fundet");

            // Shared-pool team mode (Hydra): the whole team moves together as one number.
            var affected = session.IsTeamMode && session.SharedTeamPool && player.Team is not null
                ? session.Players.Where(p => p.Team == player.Team).ToList()
                : [player];

            // No lower clamp - a killing blow should show the real overkill
            // (e.g. -37), not floor at MinPoints. Only the upper bound is a
            // real cap (can't out-heal MaxPoints).
            var before = player.Points;
            var newValue = Math.Min(player.Points + delta, session.MaxPoints);
            var actualDelta = newValue - before;
            foreach (var p in affected) p.Points = Math.Min(p.Points + actualDelta, session.MaxPoints);

            await db.SaveChangesAsync();
            await CheckForWinnerAsync(session);
        }
        finally
        {
            sessionLock.Release();
        }
    }

    private async Task CheckForWinnerAsync(GameSession session)
    {
        if (session.IsTeamMode && session.Players.Any(p => p.Team is not null))
        {
            var teamsAlive = session.Players
                .GroupBy(p => p.Team)
                .Where(g => g.Any(p => p.Points > 0))
                .ToList();

            if (session.Players.Select(p => p.Team).Distinct().Count() > 1 && teamsAlive.Count == 1)
            {
                var winningTeam = teamsAlive[0].ToList();
                await FinishGameAsync(session, winningTeam);
            }
            return;
        }

        var alive = session.Players.Where(p => p.Points > 0).ToList();
        if (session.Players.Count > 1 && alive.Count == 1)
        {
            await FinishGameAsync(session, [alive[0]]);
        }
    }

    private async Task FinishGameAsync(GameSession session, List<GamePlayer> winners)
    {
        if (session.IsCompleted) return;
        session.IsCompleted = true;

        var winnerIds = winners.Select(w => w.Id).ToHashSet();

        // Team identity only makes sense for a ranked game where every player
        // on a given side is a real, identified profile - a training player
        // (no ProfileId) can't be part of a recognizable recurring team.
        Dictionary<int, Guid> teamIdByGroup = new();
        if (session.IsRanked && session.IsTeamMode)
        {
            foreach (var group in session.Players.Where(p => p.Team is not null).GroupBy(p => p.Team!.Value))
            {
                var profileIds = group.Select(p => p.ProfileId).ToList();
                if (profileIds.All(id => id is not null))
                    teamIdByGroup[group.Key] = await GetOrCreateTeamAsync(profileIds.Select(id => id!.Value).ToList());
            }
        }

        var result = new GameResult
        {
            SessionId = session.Id,
            RulesetName = session.RulesetName,
            IsRanked = session.IsRanked,
            Players = session.Players.Select(p => new GameResultPlayer
            {
                ProfileId = p.ProfileId,
                Name = p.Name,
                FinalPoints = p.Points,
                IsWinner = winnerIds.Contains(p.Id),
                Team = p.Team,
                TeamId = p.Team is { } t && teamIdByGroup.TryGetValue(t, out var teamId) ? teamId : null
            }).ToList()
        };
        db.Results.Add(result);
        await db.SaveChangesAsync();
    }

    // Order-independent - the same group of people always resolves back to
    // the same Team row (and keeps its custom name) no matter which order
    // they joined the session in, or which side of the table they sat on.
    private async Task<Guid> GetOrCreateTeamAsync(List<Guid> profileIds)
    {
        var key = string.Join("|", profileIds.Distinct().OrderBy(id => id));
        var existing = await db.Teams.FirstOrDefaultAsync(t => t.MemberKey == key);
        if (existing is not null) return existing.Id;

        var team = new Team { MemberKey = key };
        db.Teams.Add(team);
        await db.SaveChangesAsync();
        return team.Id;
    }

    // Any current member of the team can rename it - this is a family score
    // tracker, not a permission system; the only real guard is that a
    // stranger can't rename a team they were never on.
    public async Task RenameTeamAsync(Guid teamId, string name, Guid requestingProfileId)
    {
        var team = await db.Teams.FindAsync(teamId) ?? throw new InvalidOperationException("Hold ikke fundet");
        if (!team.MemberKey.Split('|').Contains(requestingProfileId.ToString()))
            throw new InvalidOperationException("Du er ikke medlem af dette hold");

        team.Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        await db.SaveChangesAsync();
    }

    public async Task<List<TeamInfo>> GetMyTeamsAsync(Guid profileId)
    {
        var teams = await db.Teams
            .Where(t => t.MemberKey.Contains(profileId.ToString()))
            .ToListAsync();
        // Contains() above is a cheap pre-filter (SQLite has no array column
        // to query properly) - split-and-check here is what actually decides
        // membership, since a substring match could otherwise cross a "|".
        teams = teams.Where(t => t.MemberKey.Split('|').Contains(profileId.ToString())).ToList();

        var allMemberIds = teams.SelectMany(t => t.MemberKey.Split('|')).Distinct().Select(Guid.Parse).ToList();
        var profiles = await db.Profiles.Where(p => allMemberIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name);

        return teams.Select(t =>
        {
            var memberNames = t.MemberKey.Split('|').Select(id => profiles.GetValueOrDefault(Guid.Parse(id), "?"));
            return new TeamInfo(t.Id, t.Name, string.Join(" & ", memberNames));
        }).ToList();
    }

    public async Task<List<TeamLeaderboardRow>> GetTeamLeaderboardAsync(string rulesetName, DateTime? since = null)
    {
        var results = await db.Results
            .Include(r => r.Players)
            .Where(r => r.IsRanked && r.RulesetName == rulesetName && (since == null || r.CompletedAt >= since))
            .ToListAsync();

        var byTeam = new Dictionary<Guid, (int Wins, int Losses, int Draws)>();
        foreach (var result in results)
        {
            foreach (var teamGroup in result.Players.Where(p => p.TeamId is not null).GroupBy(p => p.TeamId!.Value))
            {
                var current = byTeam.TryGetValue(teamGroup.Key, out var v) ? v : (0, 0, 0);
                if (teamGroup.All(p => p.IsWinner)) current.Item1++;
                else if (teamGroup.All(p => !p.IsWinner)) current.Item2++;
                else current.Item3++; // shouldn't happen - a team wins or loses together
                byTeam[teamGroup.Key] = current;
            }
        }

        var teamIds = byTeam.Keys.ToList();
        var teams = await db.Teams.Where(t => teamIds.Contains(t.Id)).ToListAsync();
        var allMemberIds = teams.SelectMany(t => t.MemberKey.Split('|')).Distinct().Select(Guid.Parse).ToList();
        var profiles = await db.Profiles.Where(p => allMemberIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name);

        return teams
            .Select(t =>
            {
                var (wins, losses, draws) = byTeam[t.Id];
                var total = wins + losses + draws;
                var memberNames = t.MemberKey.Split('|').Select(id => profiles.GetValueOrDefault(Guid.Parse(id), "?"));
                return new TeamLeaderboardRow(
                    t.Id,
                    t.Name ?? string.Join(" & ", memberNames),
                    string.Join(" & ", memberNames),
                    wins, losses, draws, total,
                    total == 0 ? 0 : (int)Math.Round(100.0 * wins / total));
            })
            .OrderByDescending(r => r.WinRate)
            .ThenByDescending(r => r.GamesPlayed)
            .ToList();
    }

    public async Task ResetAsync(string code)
    {
        var session = await db.Sessions
            .Include(s => s.Players)
            .FirstOrDefaultAsync(s => s.Code == code.ToUpper())
            ?? throw new InvalidOperationException("Spil ikke fundet");

        foreach (var p in session.Players)
            p.Points = session.StartingPoints;
        session.IsCompleted = false;

        await db.SaveChangesAsync();
    }

    public record HeadToHeadRow(string OpponentName, int Wins, int Losses, int Draws, DateTime LastPlayed);

    public async Task<List<HeadToHeadRow>> GetStatsAsync(Guid profileId, DateTime? since = null, string? rulesetName = null)
    {
        var myRows = await db.ResultPlayers
            .Where(rp => rp.ProfileId == profileId)
            .ToListAsync();

        // Pull the full results these appeared in, then look at everyone else in each.
        // Training games are excluded - throwaway names shouldn't pollute a real
        // player's win/loss record.
        var resultIds = myRows.Select(r => r.GameResultId).ToList();
        var results = await db.Results
            .Include(r => r.Players)
            .Where(r => resultIds.Contains(r.Id) && r.IsRanked
                && (since == null || r.CompletedAt >= since)
                && (rulesetName == null || r.RulesetName == rulesetName))
            .ToListAsync();

        var byOpponent = new Dictionary<string, (int Wins, int Losses, int Draws, DateTime LastPlayed)>();

        foreach (var result in results)
        {
            var me = result.Players.FirstOrDefault(p => p.ProfileId == profileId);
            if (me is null) continue;

            foreach (var opponent in result.Players.Where(p => p.ProfileId != profileId))
            {
                var current = byOpponent.TryGetValue(opponent.Name, out var v) ? v : (0, 0, 0, DateTime.MinValue);
                var sameTeam = me.Team is not null && opponent.Team == me.Team;

                if (!sameTeam)
                {
                    if (me.IsWinner && !opponent.IsWinner) current.Item1++;
                    else if (!me.IsWinner && opponent.IsWinner) current.Item2++;
                    else current.Item3++;
                }

                if (result.CompletedAt > current.Item4) current.Item4 = result.CompletedAt;
                byOpponent[opponent.Name] = current;
            }
        }

        return byOpponent
            .Select(kv => new HeadToHeadRow(kv.Key, kv.Value.Wins, kv.Value.Losses, kv.Value.Draws, kv.Value.LastPlayed))
            .OrderByDescending(r => r.LastPlayed)
            .ToList();
    }

    public record LeaderboardRow(Guid ProfileId, string Name, string Avatar, int Wins, int Losses, int Draws, int GamesPlayed, double WinRate);

    // Name is the custom name if set, otherwise falls back to the member
    // names joined together (e.g. "ITMartin & Eigil") so a fresh team always
    // has something readable to show before anyone bothers naming it.
    public record TeamInfo(Guid Id, string? Name, string MemberNames);
    public record TeamLeaderboardRow(Guid Id, string Name, string MemberNames, int Wins, int Losses, int Draws, int GamesPlayed, double WinRate);

    // Ranked-only, real-profile players. Sorted by win rate first (ties broken
    // by games played) so someone with one lucky win doesn't read as "better"
    // than someone who's actually proven it over many games without an
    // arbitrary minimum-games cutoff hiding new players entirely.
    public async Task<List<LeaderboardRow>> GetLeaderboardAsync(DateTime? since = null, string? rulesetName = null)
    {
        var results = await db.Results
            .Include(r => r.Players)
            .Where(r => r.IsRanked
                && (since == null || r.CompletedAt >= since)
                && (rulesetName == null || r.RulesetName == rulesetName))
            .ToListAsync();

        var byProfile = new Dictionary<Guid, (int Wins, int Losses, int Draws)>();

        foreach (var result in results)
        {
            var teamsInPlay = result.Players.Select(p => p.Team).Distinct().Count() > 1;

            foreach (var rp in result.Players)
            {
                if (rp.ProfileId is not { } profileId) continue; // training/throwaway names don't rank

                // No opponents in this result (e.g. a lone all-one-team edge
                // case) - nothing to compare a win/loss against, so skip it.
                var hasOpponent = !teamsInPlay || result.Players.Any(p => p.Team != rp.Team);
                if (!hasOpponent) continue;

                var current = byProfile.TryGetValue(profileId, out var v) ? v : (0, 0, 0);
                if (rp.IsWinner) current.Item1++; else current.Item2++;
                byProfile[profileId] = current;
            }
        }

        var profileIds = byProfile.Keys.ToList();
        var profiles = await db.Profiles.Where(p => profileIds.Contains(p.Id)).ToListAsync();

        return profiles
            .Select(p =>
            {
                var (wins, losses, draws) = byProfile[p.Id];
                var played = wins + losses + draws;
                return new LeaderboardRow(p.Id, p.Name, p.Avatar, wins, losses, draws, played,
                    played == 0 ? 0 : Math.Round(100.0 * wins / played, 1));
            })
            .OrderByDescending(r => r.WinRate)
            .ThenByDescending(r => r.GamesPlayed)
            .ToList();
    }

    private static string GenerateCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string(Enumerable.Range(0, 6)
            .Select(_ => chars[Random.Shared.Next(chars.Length)])
            .ToArray());
    }
}
