using ITMartinClub.Server.Data;
using ITMartinClub.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;

namespace ITMartinClub.Server.Services;

// Session resolution used to be copy-pasted (with a real risk of drift) across
// every authenticated page. Centralizing it also means the expiry check only
// has to be added in one place instead of seven.
public sealed class ClubAuthService
{
    private readonly IConfiguration _configuration;

    public ClubAuthService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<MemberSession?> ResolveSessionAsync(IJSRuntime js, ClubDbContext db, string slug)
    {
        var sessionId = await js.InvokeAsync<string?>("clubJs.getSession");

        if (Guid.TryParse(sessionId, out var sid))
        {
            var session = await db.Sessions
                .Include(s => s.Member).ThenInclude(m => m.Group)
                .FirstOrDefaultAsync(s => s.Id == sid);

            if (session is not null && session.Member.Group.Slug == slug && session.ExpiresAt >= DateTime.UtcNow)
                return session;
        }

        // Demo tier: a real visitor never goes through /join (no invite code
        // to share, no PIN to remember), so auto-resolve to a fixed seeded
        // member instead of bouncing them into the join flow. Persisted like
        // a normal session so it survives navigation instead of re-creating
        // on every page load.
        if (_configuration.GetValue<bool>("Club:SeedDemoData") && slug == DemoSeeder.DemoSlug)
        {
            var demoMember = await db.Members
                .Include(m => m.Group)
                .Where(m => m.Group.Slug == DemoSeeder.DemoSlug)
                .OrderBy(m => m.Name)
                .FirstOrDefaultAsync(m => m.Role == "Forælder");

            if (demoMember is not null)
            {
                var demoSession = new MemberSession { MemberId = demoMember.Id };
                db.Sessions.Add(demoSession);
                await db.SaveChangesAsync();
                await js.InvokeVoidAsync("clubJs.setSession", demoSession.Id.ToString());

                demoSession.Member = demoMember;
                return demoSession;
            }
        }

        return null;
    }
}
