using ITMartinBarTab.Server.Data;
using ITMartinBarTab.Server.Data.Entities;
using ITMartinBarTab.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ITMartinBarTab.Server.Services;

public sealed class SessionService(
    BarTabDbContext db,
    IHubContext<SessionHub> hub)
{
    private static readonly string[] Colors =
    [
        "#e74c3c", "#3498db", "#2ecc71", "#f39c12",
        "#9b59b6", "#1abc9c", "#e67e22", "#e91e63"
    ];

    public async Task<Session> CreateAsync(string name)
    {
        var code = GenerateCode();
        var session = new Session { Name = name, Code = code };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    public Task<Session?> GetByCodeAsync(string code) =>
        db.Sessions
            .Include(s => s.Participants)
            .Include(s => s.Drinks)
                .ThenInclude(d => d.Shares)
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.Code == code.ToUpper());

    public async Task<Participant> JoinAsync(string sessionCode, string name)
    {
        var session = await db.Sessions
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.Code == sessionCode.ToUpper())
            ?? throw new InvalidOperationException("Session not found");

        var color = Colors[session.Participants.Count % Colors.Length];
        var participant = new Participant
        {
            SessionId = session.Id,
            Name = name,
            Color = color
        };

        db.Participants.Add(participant);
        await db.SaveChangesAsync();

        await hub.Clients.Group(sessionCode.ToUpper()).SendAsync("ParticipantJoined", new
        {
            participant.Id,
            participant.Name,
            participant.Color
        });

        return participant;
    }

    public async Task<DrinkEntry> AddDrinkAsync(
        string sessionCode,
        Guid participantId,
        string description,
        decimal price,
        bool isRound,
        string? photoPath,
        Dictionary<Guid, ShareType> shares)
    {
        var session = await db.Sessions
            .FirstOrDefaultAsync(s => s.Code == sessionCode.ToUpper())
            ?? throw new InvalidOperationException("Session not found");

        var drink = new DrinkEntry
        {
            SessionId = session.Id,
            AddedByParticipantId = participantId,
            Description = description,
            Price = price,
            IsRound = isRound,
            PhotoPath = photoPath,
            Shares = shares.Select(kv => new DrinkShare
            {
                ParticipantId = kv.Key,
                Share = kv.Value
            }).ToList()
        };

        db.Drinks.Add(drink);
        await db.SaveChangesAsync();

        await hub.Clients.Group(sessionCode.ToUpper()).SendAsync("DrinkAdded", new
        {
            drink.Id,
            drink.Description,
            drink.Price,
            drink.IsRound,
            drink.PhotoPath,
            drink.CreatedAt,
            AddedBy = participantId,
            Shares = shares.Select(kv => new { ParticipantId = kv.Key, Share = kv.Value.ToString() })
        });

        return drink;
    }

    public async Task UpdateShareAsync(string sessionCode, Guid drinkId, Guid participantId, ShareType share)
    {
        var existing = await db.DrinkShares
            .FirstOrDefaultAsync(s => s.DrinkEntryId == drinkId && s.ParticipantId == participantId);

        if (existing is null)
        {
            db.DrinkShares.Add(new DrinkShare
            {
                DrinkEntryId = drinkId,
                ParticipantId = participantId,
                Share = share
            });
        }
        else
        {
            existing.Share = share;
        }

        await db.SaveChangesAsync();

        await hub.Clients.Group(sessionCode.ToUpper()).SendAsync("ShareUpdated", new
        {
            DrinkId = drinkId,
            ParticipantId = participantId,
            Share = share.ToString()
        });
    }

    public async Task<ChatMessage> SendMessageAsync(string sessionCode, Guid participantId, string text, Guid? drinkId = null)
    {
        var session = await db.Sessions
            .FirstOrDefaultAsync(s => s.Code == sessionCode.ToUpper())
            ?? throw new InvalidOperationException("Session not found");

        var msg = new ChatMessage
        {
            SessionId = session.Id,
            ParticipantId = participantId,
            Text = text,
            DrinkEntryId = drinkId
        };

        db.ChatMessages.Add(msg);
        await db.SaveChangesAsync();

        var participant = await db.Participants.FindAsync(participantId);

        await hub.Clients.Group(sessionCode.ToUpper()).SendAsync("MessageReceived", new
        {
            msg.Id,
            msg.Text,
            msg.CreatedAt,
            msg.DrinkEntryId,
            ParticipantId = participantId,
            ParticipantName = participant?.Name ?? "?",
            ParticipantColor = participant?.Color ?? "#999"
        });

        return msg;
    }

    private static string GenerateCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string(Enumerable.Range(0, 6)
            .Select(_ => chars[Random.Shared.Next(chars.Length)])
            .ToArray());
    }
}
