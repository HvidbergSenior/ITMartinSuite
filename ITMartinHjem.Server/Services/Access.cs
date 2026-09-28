using System.Security.Claims;

namespace ITMartinHjem.Server.Services;

// Two roles in one cookie: "familie" (sees family posts) and "ejer" (also edits + answers chat).
public static class Access
{
    public const string Owner = "ejer";
    public const string Family = "familie";

    public static bool IsOwner(this ClaimsPrincipal? u) => u?.IsInRole(Owner) == true;
    public static bool IsFamily(this ClaimsPrincipal? u) => u?.IsInRole(Family) == true || u.IsOwner();
}
