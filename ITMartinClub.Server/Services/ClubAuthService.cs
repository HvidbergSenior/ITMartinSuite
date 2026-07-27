using ITMartinClub.Server.Data;
using ITMartinClub.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;

namespace ITMartinClub.Server.Services;

// Session resolution used to be copy-pasted (with a real risk of drift) across
// every authenticated page. Centralizing it also means the expiry check only
// has to be added in one place instead of seven.
public sealed class ClubAuthService
{
    public async Task<MemberSession?> ResolveSessionAsync(IJSRuntime js, ClubDbContext db, string slug)
    {
        var sessionId = await js.InvokeAsync<string?>("clubJs.getSession");
        if (!Guid.TryParse(sessionId, out var sid)) return null;

        var session = await db.Sessions
            .Include(s => s.Member).ThenInclude(m => m.Group)
            .FirstOrDefaultAsync(s => s.Id == sid);

        if (session is null || session.Member.Group.Slug != slug || session.ExpiresAt < DateTime.UtcNow)
            return null;

        return session;
    }
}
