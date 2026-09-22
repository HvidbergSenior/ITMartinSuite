using System.Security.Claims;
using ITMartinMitEl.Server.Data;

namespace ITMartinMitEl.Server.Services;

/// <summary>
/// Which household the current request is about. For an owner that is their own home;
/// for an advisor it is the customer they picked, and only while that customer's grant
/// is live. Every page and store resolution goes through here, so a household can never
/// be reached by guessing an id in a url.
/// </summary>
public sealed class TenantContext(IHttpContextAccessor accessor, AdvisorService advisors)
{
    public const string HouseholdCookie = "mitel_household";

    public ClaimsPrincipal? User => accessor.HttpContext?.User;

    public Guid? UserId =>
        Guid.TryParse(User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    public string Role => User?.FindFirst(ClaimTypes.Role)?.Value ?? UserRoles.Owner;

    public bool IsAdvisor => Role == UserRoles.Advisor;

    public string Email => User?.FindFirst(ClaimTypes.Email)?.Value ?? "";

    public string DisplayName => User?.FindFirst(ClaimTypes.Name)?.Value ?? Email;

    /// <summary>The owner's own household, straight from the login cookie.</summary>
    public Guid? OwnHouseholdId =>
        Guid.TryParse(User?.FindFirst("household")?.Value, out var id) ? id : null;

    /// <summary>The household an advisor has selected, if any.</summary>
    public Guid? SelectedHouseholdId =>
        Guid.TryParse(accessor.HttpContext?.Request.Cookies[HouseholdCookie], out var id) ? id : null;

    /// <summary>
    /// The household to show. Never returns a customer's household to an advisor who has
    /// no live grant for it - a revoked customer disappears on the next request.
    /// </summary>
    public async Task<Guid?> CurrentHouseholdIdAsync()
    {
        if (!IsAdvisor) return OwnHouseholdId;
        if (UserId is not { } advisorId) return null;
        if (SelectedHouseholdId is not { } selected) return null;

        return await advisors.HasAccessAsync(advisorId, selected) ? selected : null;
    }

    /// <summary>Synchronous twin of <see cref="CurrentHouseholdIdAsync"/>, used when the stores are resolved.</summary>
    public Guid? CurrentHouseholdId()
    {
        if (!IsAdvisor) return OwnHouseholdId;
        if (UserId is not { } advisorId) return null;
        if (SelectedHouseholdId is not { } selected) return null;

        return advisors.HasAccess(advisorId, selected) ? selected : null;
    }

    public void SelectHousehold(Guid householdId)
    {
        accessor.HttpContext?.Response.Cookies.Append(HouseholdCookie, householdId.ToString(), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(30),
        });
    }

    public void ClearHousehold() =>
        accessor.HttpContext?.Response.Cookies.Delete(HouseholdCookie);
}
