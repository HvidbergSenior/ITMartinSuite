using ITMartinClub.Application;
using ITMartinClub.Domain;
using Microsoft.JSInterop;

namespace ITMartinClub.Server.Services;

// Which member this browser is: reads the session id from browser storage (clubJs) and the server cookie, lets
// TeamAccess decide, and writes the winner back to browser storage. Every logged-in page starts here.
public sealed class ClubAuthService(TeamAccess access, IHttpContextAccessor http)
{
    public async Task<MemberSession?> ResolveSessionAsync(IJSRuntime js, string slug)
    {
        var stored = await js.InvokeAsync<string?>("clubJs.getSession");
        string? cookie = null;
        http.HttpContext?.Request.Cookies.TryGetValue("club_session_srv", out cookie);

        var result = await access.ResolveAsync(slug, stored, cookie);
        if (result is null) return null;
        if (result.StoreInBrowser) await js.InvokeVoidAsync("clubJs.setSession", result.Session.Id.ToString());
        return result.Session;
    }
}
