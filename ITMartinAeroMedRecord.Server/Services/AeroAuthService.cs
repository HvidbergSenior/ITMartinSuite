using ITMartinAeroMedRecord.Server.Data;
using ITMartinAeroMedRecord.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;

namespace ITMartinAeroMedRecord.Server.Services;

// Session resolution centralized in one place (same reasoning as Club's
// ClubAuthService) so the expiry check only has to live once.
public sealed class AeroAuthService
{
    public async Task<MemberSession?> ResolveSessionAsync(IJSRuntime js, AeroDbContext db, string slug)
    {
        var sessionId = await js.InvokeAsync<string?>("aeroJs.getSession");

        if (!Guid.TryParse(sessionId, out var sid))
            return null;

        var session = await db.Sessions
            .Include(s => s.Member).ThenInclude(m => m.Group)
            .FirstOrDefaultAsync(s => s.Id == sid);

        if (session is not null && session.Member.Group.Slug == slug && session.ExpiresAt >= DateTime.UtcNow)
            return session;

        return null;
    }

    // Root "/" (CreateGroup) doesn't know a slug up front - it just needs to
    // know whether *any* valid session exists so it can redirect straight
    // into that group instead of always showing the create-team screen.
    public async Task<MemberSession?> ResolveAnySessionAsync(IJSRuntime js, AeroDbContext db)
    {
        var sessionId = await js.InvokeAsync<string?>("aeroJs.getSession");

        if (!Guid.TryParse(sessionId, out var sid))
            return null;

        var session = await db.Sessions
            .Include(s => s.Member).ThenInclude(m => m.Group)
            .FirstOrDefaultAsync(s => s.Id == sid);

        return session is not null && session.ExpiresAt >= DateTime.UtcNow ? session : null;
    }
}
