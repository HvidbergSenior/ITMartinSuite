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
            DefaultStartingPoints = startingPoints > 0 ? startingPoints : 50,
            IsBuiltIn = false,
            CreatedByProfileName = createdByName
        };
        db.Rulesets.Add(ruleset);
        await db.SaveChangesAsync();
        return ruleset;
    }

    public Task<PlayerProfile?> FindProfileAsync(string deviceToken) =>
        db.Profiles.FirstOrDefaultAsync(p => p.DeviceToken == deviceToken);

    public async Task<PlayerProfile> GetOrCreateProfileAsync(string deviceToken, string name, string avatar)
    {
        var existing = await db.Profiles.FirstOrDefaultAsync(p => p.DeviceToken == deviceToken);
        if (existing is not null)
        {
            if (!string.IsNullOrWhiteSpace(name)) existing.Name = name.Trim();
            if (!string.IsNullOrWhiteSpace(avatar)) existing.Avatar = avatar;
            await db.SaveChangesAsync();
            return existing;
        }

        var profile = new PlayerProfile
        {
            DeviceToken = deviceToken,
            Name = string.IsNullOrWhiteSpace(name) ? "Spiller" : name.Trim(),
            Avatar = string.IsNullOrWhiteSpace(avatar) ? "🚀" : avatar
        };
        db.Profiles.Add(profile);
        await db.SaveChangesAsync();
        return profile;
    }

    public async Task<GameSession> CreateAsync(Guid rulesetId, int startingPoints)
    {
        var ruleset = await db.Rulesets.FindAsync(rulesetId)
            ?? throw new InvalidOperationException("Regelsæt ikke fundet");

        var session = new GameSession
        {
            Code = GenerateCode(),
            StartingPoints = startingPoints > 0 ? startingPoints : ruleset.DefaultStartingPoints,
            RulesetId = ruleset.Id,
            RulesetName = ruleset.Name,
            IsTeamMode = ruleset.IsTeamMode,
            SharedTeamPool = ruleset.SharedTeamPool
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

        // First player to join a fresh session opens the game and goes first.
        if (session.Players.Count == 0)
            session.CurrentTurnPlayerId = player.Id;

        await db.SaveChangesAsync();
        return player;
    }

    public async Task AdjustPointsAsync(string code, Guid playerId, int delta)
    {
        var session = await db.Sessions
            .Include(s => s.Players)
            .FirstOrDefaultAsync(s => s.Code == code.ToUpper())
            ?? throw new InvalidOperationException("Spil ikke fundet");

        var player = session.Players.FirstOrDefault(p => p.Id == playerId)
            ?? throw new InvalidOperationException("Spiller ikke fundet");

        // Shared-pool team mode (Hydra): the whole team moves together as one number.
        var affected = session.IsTeamMode && session.SharedTeamPool && player.Team is not null
            ? session.Players.Where(p => p.Team == player.Team).ToList()
            : [player];

        var before = player.Points;
        var newValue = Math.Clamp(player.Points + delta, session.MinPoints, session.MaxPoints);
        var actualDelta = newValue - before;
        foreach (var p in affected) p.Points = Math.Clamp(p.Points + actualDelta, session.MinPoints, session.MaxPoints);

        db.Events.Add(new GameEvent
        {
            SessionId = session.Id,
            PlayerId = player.Id,
            PlayerName = player.Name,
            PlayerAvatar = player.Avatar,
            Delta = actualDelta,
            ResultingPoints = player.Points
        });

        await db.SaveChangesAsync();
        await CheckForWinnerAsync(session);
    }

    public Task<List<GameEvent>> GetRecentEventsAsync(Guid sessionId, int take = 25) =>
        db.Events.Where(e => e.SessionId == sessionId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(take)
            .ToListAsync();

    public async Task NextTurnAsync(string code, Guid requestingPlayerId)
    {
        var session = await db.Sessions
            .Include(s => s.Players)
            .FirstOrDefaultAsync(s => s.Code == code.ToUpper())
            ?? throw new InvalidOperationException("Spil ikke fundet");

        // Only the player whose turn it currently is may pass it on (or anyone, the
        // very first time, if turn tracking hasn't started yet on an old session).
        if (session.CurrentTurnPlayerId is not null && session.CurrentTurnPlayerId != requestingPlayerId)
            throw new InvalidOperationException("Det er ikke din tur");

        var ordered = session.Players.OrderBy(p => p.SortOrder).ToList();
        if (ordered.Count == 0) return;

        var currentIndex = ordered.FindIndex(p => p.Id == requestingPlayerId);
        if (currentIndex < 0) currentIndex = 0;

        GamePlayer next = ordered[(currentIndex + 1) % ordered.Count];
        for (var i = 1; i <= ordered.Count; i++)
        {
            var candidate = ordered[(currentIndex + i) % ordered.Count];
            if (candidate.Points > session.MinPoints) { next = candidate; break; }
        }

        session.CurrentTurnPlayerId = next.Id;
        await db.SaveChangesAsync();
    }

    private async Task CheckForWinnerAsync(GameSession session)
    {
        if (session.IsTeamMode && session.Players.Any(p => p.Team is not null))
        {
            var teamsAlive = session.Players
                .GroupBy(p => p.Team)
                .Where(g => g.Any(p => p.Points > session.MinPoints))
                .ToList();

            if (session.Players.Select(p => p.Team).Distinct().Count() > 1 && teamsAlive.Count == 1)
            {
                var winningTeam = teamsAlive[0].ToList();
                await FinishGameAsync(session, winningTeam);
            }
            return;
        }

        var alive = session.Players.Where(p => p.Points > session.MinPoints).ToList();
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
        var result = new GameResult
        {
            SessionId = session.Id,
            RulesetName = session.RulesetName,
            Players = session.Players.Select(p => new GameResultPlayer
            {
                ProfileId = p.ProfileId,
                Name = p.Name,
                FinalPoints = p.Points,
                IsWinner = winnerIds.Contains(p.Id),
                Team = p.Team
            }).ToList()
        };
        db.Results.Add(result);
        await db.SaveChangesAsync();
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
        session.CurrentTurnPlayerId = session.Players.OrderBy(p => p.SortOrder).FirstOrDefault()?.Id;

        var oldEvents = db.Events.Where(e => e.SessionId == session.Id);
        db.Events.RemoveRange(oldEvents);

        await db.SaveChangesAsync();
    }

    public record HeadToHeadRow(string OpponentName, int Wins, int Losses, int Draws, DateTime LastPlayed);

    public async Task<List<HeadToHeadRow>> GetStatsAsync(Guid profileId, DateTime? since = null)
    {
        var myRows = await db.ResultPlayers
            .Where(rp => rp.ProfileId == profileId)
            .ToListAsync();

        // Pull the full results these appeared in, then look at everyone else in each.
        var resultIds = myRows.Select(r => r.GameResultId).ToList();
        var results = await db.Results
            .Include(r => r.Players)
            .Where(r => resultIds.Contains(r.Id) && (since == null || r.CompletedAt >= since))
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

    private static string GenerateCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string(Enumerable.Range(0, 6)
            .Select(_ => chars[Random.Shared.Next(chars.Length)])
            .ToArray());
    }
}
