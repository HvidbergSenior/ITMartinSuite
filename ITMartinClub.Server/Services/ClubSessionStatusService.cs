using ITMartinClub.Server.Data;
using ITMartinClub.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinClub.Server.Services;

// Single source of truth for "is there a live play session right now, and what
// does it look like" - previously computed inline in ClubNav.razor only; now
// also needed by GroupHome.razor's live status card, so it lives here instead
// of being copy-pasted a second time.
public sealed class ClubSessionStatusService(ClubDbContext db)
{
    public async Task<SessionStatus?> GetActiveSessionAsync(Guid groupId)
    {
        var session = await db.PlaySessions
            .Where(p => p.GroupId == groupId && p.EndedAt == null)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();

        if (session is null) return null;

        string label;
        switch (session.Phase)
        {
            case "Invitations":
                var activeCheckIds = await db.ReadyChecks
                    .Where(rc => rc.GroupId == groupId && rc.ExpiresAt > DateTime.UtcNow)
                    .Select(rc => rc.Id)
                    .ToListAsync();
                var readyCount = activeCheckIds.Count == 0 ? 0 : await db.ReadyCheckResponses
                    .CountAsync(r => activeCheckIds.Contains(r.ReadyCheckId) && r.Status == "Ready");
                label = readyCount > 0 ? $"📋 Inviterer - {readyCount} klar" : "📋 Inviterer";
                break;
            case "Playing":
                var todayUtc = DateTime.UtcNow.Date;
                var matchCount = await db.Matches.CountAsync(m => m.GroupId == groupId && m.CreatedAt >= todayUtc);
                label = matchCount > 0 ? $"🎮 Spiller - {matchCount} {(matchCount == 1 ? "kamp" : "kampe")}" : "🎮 Spiller";
                break;
            default:
                label = "🎬 Opsummerer";
                break;
        }

        var cssClass = session.Phase switch { "Invitations" => "pre", "Playing" => "now", _ => "post" };
        return new SessionStatus(session, cssClass, label);
    }

    // Members who responded with the given status to any ready-check created
    // since this session started - used both to suppress further pushes to
    // people who declined, and to target live highlight pushes at spectators.
    public async Task<List<string>> GetMembersWithStatusAsync(Guid groupId, PlaySession session, string status)
    {
        var checkIds = await db.ReadyChecks
            .Where(rc => rc.GroupId == groupId && rc.CreatedAt >= session.CreatedAt)
            .Select(rc => rc.Id)
            .ToListAsync();
        if (checkIds.Count == 0) return [];

        return await db.ReadyCheckResponses
            .Where(r => checkIds.Contains(r.ReadyCheckId) && r.Status == status)
            .Select(r => r.MemberName)
            .Distinct()
            .ToListAsync();
    }
}

public sealed record SessionStatus(PlaySession Session, string CssClass, string Label);
