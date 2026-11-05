using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ITMartinIndbakke.Application;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;

namespace ITMartinIndbakke.Infrastructure;

/// <summary>
/// "Log ind med Microsoft" for personal Outlook.com / Hotmail / Live accounts (2026-11-05, user: "azure ok, build outlook").
/// Microsoft identity platform, /consumers (personal accounts only), authorization code + PKCE. The app registration lives in
/// Martin's Microsoft account (Indbakke__MicrosoftClientId / __MicrosoftSecret). The tickets are kept in memory only.
/// </summary>
public sealed class MicrosoftLogin(HttpClient http, IHttpClientFactory clients, string clientId, string secret, string redirectUri,
    ILogger<MicrosoftLogin> log) : IMicrosoftLogin
{
    private const string Authority = "https://login.microsoftonline.com/consumers/oauth2/v2.0";
    internal const string Scopes = "offline_access openid email https://outlook.office.com/IMAP.AccessAsUser.All https://outlook.office.com/SMTP.Send";

    public bool IsConfigured => clientId.Length > 0 && secret.Length > 0;

    public string AuthorizeUrl(string state, string codeChallenge) =>
        $"{Authority}/authorize?client_id={Uri.EscapeDataString(clientId)}&response_type=code&response_mode=query" +
        $"&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope={Uri.EscapeDataString(Scopes)}&state={state}" +
        $"&code_challenge={codeChallenge}&code_challenge_method=S256&prompt=select_account";

    private sealed record TokenAnswer(string? access_token, string? refresh_token, int expires_in, string? id_token, string? error, string? error_description);

    public async Task<(string? Email, MailAccess? Access, string? Error)> FinishAsync(string authCode, string codeVerifier, CancellationToken ct)
    {
        var t = await TokenAsync(new()
        {
            ["client_id"] = clientId, ["client_secret"] = secret, ["grant_type"] = "authorization_code", ["code"] = authCode,
            ["redirect_uri"] = redirectUri, ["code_verifier"] = codeVerifier, ["scope"] = Scopes,
        }, ct);
        if (t?.access_token is null) return (null, null, "Microsoft gav ikke adgang. Prøv igen – og tryk Ja/Tillad hos Microsoft.");
        var email = EmailFrom(t.id_token);
        if (email is null) return (null, null, "Microsoft fortalte ikke, hvilken mail det er. Prøv igen.");

        var ticket = new Ticket(this, t.access_token, t.refresh_token, DateTimeOffset.UtcNow.AddSeconds(t.expires_in));
        var outlook = new OutlookImap(email, ticket.GetAsync);
        var unsubscribe = new Unsubscriber(clients.CreateClient("unsubscribe"), email, "",
            async (smtp, c) => await smtp.AuthenticateAsync(new SaslMechanismOAuth2(email, await ticket.GetAsync(c)), c),
            "smtp-mail.outlook.com", 587, SecureSocketOptions.StartTls);
        return (email, new MailAccess(outlook, outlook, unsubscribe), null);
    }

    private Task<TokenAnswer?> RefreshAsync(string refresh, CancellationToken ct) => TokenAsync(new()
    {
        ["client_id"] = clientId, ["client_secret"] = secret, ["grant_type"] = "refresh_token", ["refresh_token"] = refresh, ["scope"] = Scopes,
    }, ct);

    private async Task<TokenAnswer?> TokenAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        try
        {
            using var res = await http.PostAsync($"{Authority}/token", new FormUrlEncodedContent(form), ct);
            var t = await res.Content.ReadFromJsonAsync<TokenAnswer>(ct);
            if (!res.IsSuccessStatusCode) log.LogWarning("Microsoft token: {Status} {Error}", (int)res.StatusCode, t?.error);
            return t;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            log.LogWarning(e, "Microsoft token call failed");
            return null;
        }
    }

    /// <summary>The address from the id_token (its middle part is plain JSON) - "email", else "preferred_username".</summary>
    internal static string? EmailFrom(string? idToken)
    {
        var parts = idToken?.Split('.');
        if (parts is not { Length: >= 2 }) return null;
        var b64 = parts[1].Replace('-', '+').Replace('_', '/');
        b64 = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');
        try
        {
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(b64)));
            foreach (var claim in new[] { "email", "preferred_username" })
                if (doc.RootElement.TryGetProperty(claim, out var v) && v.GetString() is { } e && e.Contains('@')) return e.Trim().ToLowerInvariant();
        }
        catch (Exception e) when (e is FormatException or JsonException) { }
        return null;
    }

    /// <summary>The access ticket lasts about an hour; it is renewed with the refresh ticket while the person uses the page.</summary>
    private sealed class Ticket(MicrosoftLogin owner, string access, string? refresh, DateTimeOffset expires)
    {
        private readonly SemaphoreSlim _gate = new(1, 1);

        public async Task<string> GetAsync(CancellationToken ct)
        {
            if (DateTimeOffset.UtcNow < expires.AddMinutes(-3) || refresh is null) return access;
            await _gate.WaitAsync(ct);
            try
            {
                if (DateTimeOffset.UtcNow < expires.AddMinutes(-3)) return access;
                var t = await owner.RefreshAsync(refresh, ct);
                if (t?.access_token is null) throw new InvalidOperationException("Microsoft-login er udløbet. Log ind igen.");
                access = t.access_token; refresh = t.refresh_token ?? refresh; expires = DateTimeOffset.UtcNow.AddSeconds(t.expires_in);
                return access;
            }
            finally { _gate.Release(); }
        }
    }
}
