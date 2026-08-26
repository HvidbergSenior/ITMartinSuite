using ITMartinRedigerDokument.Server.Data;
using ITMartinRedigerDokument.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;

namespace ITMartinRedigerDokument.Server.Services;

// Session resolution centralized in one place (same reasoning as Club's
// ClubAuthService) so the expiry check only has to live once.
public sealed class RedigerAuthService
{
    public async Task<MemberSession?> ResolveSessionAsync(IJSRuntime js, RedigerDbContext db, string slug)
    {
        var sessionId = await js.InvokeAsync<string?>("redigerJs.getSession");

        if (!Guid.TryParse(sessionId, out var sid))
            return null;

        var session = await db.Sessions
            .Include(s => s.Member).ThenInclude(m => m.Group)
            .FirstOrDefaultAsync(s => s.Id == sid);

        if (session is not null && session.Member.Group.Slug == slug && session.ExpiresAt >= DateTime.UtcNow)
            return session;

        return null;
    }
}
