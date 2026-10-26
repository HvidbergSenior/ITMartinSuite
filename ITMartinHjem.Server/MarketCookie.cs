using Microsoft.AspNetCore.DataProtection;

namespace ITMartinHjem.Server;

/// <summary>The customer's kundenummer after logging into the marketplace (/kunde): kept in an HttpOnly cookie,
/// data-protected so nobody can type another person's number into it. Only read while Hjem__MarketForCustomers=true.</summary>
public sealed class MarketCookie(IDataProtectionProvider dp, ITMartinHjem.Application.ShopVisibility visibility)
{
    public const string Name = "bs_kunde";
    private IDataProtector Protector => dp.CreateProtector("bs_kunde.v1");

    /// <summary>"" when the marketplace is closed to customers, nobody is logged in, or the cookie is not ours.</summary>
    public string Read(HttpContext? ctx)
    {
        if (!visibility.Customers || ctx?.Request.Cookies[Name] is not { Length: > 0 } raw) return "";
        try { return Protector.Unprotect(raw); }
        catch (System.Security.Cryptography.CryptographicException) { return ""; }
    }

    public void Write(HttpContext ctx, string customer) =>
        ctx.Response.Cookies.Append(Name, Protector.Protect(customer), new CookieOptions
        {
            HttpOnly = true, Secure = ctx.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(180),
        });

    public static void Clear(HttpContext ctx) => ctx.Response.Cookies.Delete(Name);
}
