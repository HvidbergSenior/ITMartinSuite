using ITMartinClub.Server.Data;
using ITMartinClub.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;

namespace ITMartinClub.Server.Services;

// Session resolution used to be copy-pasted (with a real risk of drift) across
// every authenticated page. Centralizing it also means the expiry check only
// has to be added in one place instead of seven.
public sealed class ClubAuthService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _http;

    public ClubAuthService(IConfiguration configuration, IHttpContextAccessor http)
    {
        _configuration = configuration;
        _http = http;
    }

    public async Task<MemberSession?> ResolveSessionAsync(IJSRuntime js, ClubDbContext db, string slug)
    {
        var sessionId = await js.InvokeAsync<string?>("clubJs.getSession");

        // Browser storage gone (iOS wipes it after 7 quiet days)? The
        // server-set cookie from /api/session still knows who this is.
        var fromServerCookie = false;
        if (!Guid.TryParse(sessionId, out _) && _http.HttpContext?.Request.Cookies.TryGetValue("club_session_srv", out var srv) == true)
        {
            sessionId = srv;
            fromServerCookie = true;
        }

        if (Guid.TryParse(sessionId, out var sid))
        {
            var session = await db.Sessions
                .Include(s => s.Member).ThenInclude(m => m.Group)
                .FirstOrDefaultAsync(s => s.Id == sid);

            if (session is not null && session.Member.Group.Slug == slug && session.ExpiresAt >= DateTime.UtcNow)
            {
                // Sliding: anyone who shows up keeps their login another year.
                if (session.ExpiresAt < DateTime.UtcNow.AddDays(300))
                {
                    session.ExpiresAt = DateTime.UtcNow.AddDays(365);
                    await db.SaveChangesAsync();
                }
                if (fromServerCookie) await js.InvokeVoidAsync("clubJs.setSession", session.Id.ToString());
                return session;
            }
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
