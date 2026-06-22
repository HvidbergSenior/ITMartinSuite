using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

namespace ITMartinTestHub.Server.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminAuthController(IConfiguration config) : ControllerBase
{
    private const string CookieName = "th_admin";

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest req)
    {
        var pin = config["Admin:Pin"] ?? "1234";
        if (req.Pin != pin) return Unauthorized();

        Response.Cookies.Append(CookieName, Token(pin), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Expires  = DateTimeOffset.UtcNow.AddDays(30)
        });
        return Ok();
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(CookieName);
        return Ok();
    }

    internal static string Token(string pin)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes("th_admin_" + pin));
        return Convert.ToHexString(bytes)[..16];
    }
}

public record LoginRequest(string Pin);
