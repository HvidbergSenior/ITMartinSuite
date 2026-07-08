using ITMartinStarRealms.Server.Data;
using ITMartinStarRealms.Server.Data.Entities;
using ITMartinStarRealms.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ITMartinStarRealms.Server.Services;

public sealed class GameService(StarRealmsDbContext db, IHubContext<GameHub> hub)
{
    public const int MaxPlayers = 6;

    private static readonly string[] Colors =
    [
        "#e74c3c", "#3498db", "#2ecc71", "#f1c40f", "#9b59b6", "#e67e22"
    ];

    public async Task<GameSession> CreateAsync(int startingPoints)
    {
        var session = new GameSession { Code = GenerateCode(), StartingPoints = startingPoints };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    public Task<GameSession?> GetByCodeAsync(string code) =>
        db.Sessions
            .Include(s => s.Players)
            .FirstOrDefaultAsync(s => s.Code == code.ToUpper());

    public async Task<GamePlayer> GetOrCreatePlayerAsync(string code, string token, string name)
    {
        var session = await db.Sessions
            .Include(s => s.Players)
            .FirstOrDefaultAsync(s => s.Code == code.ToUpper())
            ?? throw new InvalidOperationException("Spil ikke fundet");

        var existing = session.Players.FirstOrDefault(p => p.Token == token);
        if (existing is not null) return existing;

        if (session.Players.Count >= MaxPlayers)
            throw new InvalidOperationException("Spillet er fuldt (maks 6 spillere)");

        var player = new GamePlayer
        {
            SessionId  = session.Id,
            Token      = token,
            Name       = string.IsNullOrWhiteSpace(name) ? $"Spiller {session.Players.Count + 1}" : name.Trim(),
            Color      = Colors[session.Players.Count % Colors.Length],
            Points     = session.StartingPoints,
            SortOrder  = session.Players.Count
        };
        db.Players.Add(player);
        await db.SaveChangesAsync();

        await hub.Clients.Group(code.ToUpper()).SendAsync("PlayerJoined", new
        {
            player.Id, player.Name, player.Color, player.Points, player.SortOrder
        });

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

        var before = player.Points;
        player.Points = Math.Clamp(player.Points + delta, session.MinPoints, session.MaxPoints);
        await db.SaveChangesAsync();

        await hub.Clients.Group(code.ToUpper()).SendAsync("PointsUpdated", new
        {
            player.Id,
            player.Points,
            Delta = player.Points - before
        });

        var alive = session.Players.Where(p => p.Points > session.MinPoints).ToList();
        if (session.Players.Count > 1 && alive.Count == 1)
        {
            var winner = alive[0];
            await hub.Clients.Group(code.ToUpper()).SendAsync("GameOver", new
            {
                winner.Id, winner.Name, winner.Color
            });
        }
    }

    public async Task ResetAsync(string code)
    {
        var session = await db.Sessions
            .Include(s => s.Players)
            .FirstOrDefaultAsync(s => s.Code == code.ToUpper())
            ?? throw new InvalidOperationException("Spil ikke fundet");

        foreach (var p in session.Players)
            p.Points = session.StartingPoints;
        await db.SaveChangesAsync();

        await hub.Clients.Group(code.ToUpper()).SendAsync("GameReset", session.StartingPoints);
    }

    private static string GenerateCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string(Enumerable.Range(0, 6)
            .Select(_ => chars[Random.Shared.Next(chars.Length)])
            .ToArray());
    }
}
