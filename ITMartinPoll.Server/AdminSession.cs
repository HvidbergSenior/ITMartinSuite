namespace ITMartinPoll.Server;

public class AdminSession
{
    public bool IsAdmin { get; set; }
}

// Cookie value for the plain (non-Blazor) admin-only URLs such as the package PDF: a hash of the admin PIN,
// so the PIN itself never sits in a cookie.
public static class AdminToken
{
    public static string For(string pin) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("poll-admin|" + pin)));
}
