using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using ITMartinSnak.Application;

namespace ITMartinSnak.Infrastructure;

/// <summary>
/// Fetches a web page's title for Idéer. Safe by design: http/https only, at most 300 KB read, 6 seconds, and every connection
/// (also after a redirect) goes only to a PUBLIC address - a pasted link can never reach the NAS, the router or the photoserver.
/// </summary>
public sealed partial class HttpPageTitles : IPageTitles, IDisposable
{
    private const int MaxBytes = 300 * 1024;
    private readonly HttpClient _http;

    public HttpPageTitles()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 3,
            ConnectTimeout = TimeSpan.FromSeconds(4),
            ConnectCallback = ConnectPublicOnlyAsync,
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(6) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; SnakIdeer/1.0)");
        _http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("da-DK,da;q=0.9,en;q=0.5");
    }

    public async Task<string?> TitleAsync(string url, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) return null;
        try
        {
            using var res = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!res.IsSuccessStatusCode || res.Content.Headers.ContentType?.MediaType is not ("text/html" or "application/xhtml+xml")) return null;
            await using var s = await res.Content.ReadAsStreamAsync(ct);
            var buf = new byte[MaxBytes];
            int total = 0, n;
            while (total < MaxBytes && (n = await s.ReadAsync(buf.AsMemory(total, MaxBytes - total), ct)) > 0) total += n;
            return TitleIn(Encoding.UTF8.GetString(buf, 0, total));
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException or SocketException or InvalidOperationException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"<meta[^>]+property=[""']og:title[""'][^>]+content=[""']([^""']+)[""']", RegexOptions.IgnoreCase)] private static partial Regex OgTitle();
    [GeneratedRegex(@"<meta[^>]+content=[""']([^""']+)[""'][^>]+property=[""']og:title[""']", RegexOptions.IgnoreCase)] private static partial Regex OgTitleRev();
    [GeneratedRegex(@"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)] private static partial Regex TitleTag();

    /// <summary>og:title first (what the page wants shown when shared), else &lt;title&gt;.</summary>
    public static string? TitleIn(string html)
    {
        var m = OgTitle().Match(html);
        if (!m.Success) m = OgTitleRev().Match(html);
        if (!m.Success) m = TitleTag().Match(html);
        if (!m.Success) return null;
        var t = WebUtility.HtmlDecode(m.Groups[1].Value).Trim();
        return t.Length == 0 ? null : t;
    }

    private static async ValueTask<Stream> ConnectPublicOnlyAsync(SocketsHttpConnectionContext ctx, CancellationToken ct)
    {
        var addresses = await Dns.GetHostAddressesAsync(ctx.DnsEndPoint.Host, ct);
        var ip = addresses.FirstOrDefault(IsPublic) ?? throw new HttpRequestException("Not a public address");
        var socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(new IPEndPoint(ip, ctx.DnsEndPoint.Port), ct);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>False for loopback, private, link-local, carrier-grade NAT, multicast and other non-internet ranges.</summary>
    public static bool IsPublic(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip)) return false;
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return !(b[0] is 0 or 10 or 127 || b[0] >= 224
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
                || (b[0] == 169 && b[1] == 254)
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 192 && b[1] == 0 && b[2] is 0 or 2)
                || (b[0] == 198 && b[1] is 18 or 19));
        }
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast || ip.Equals(IPAddress.IPv6None)) return false;
            var b = ip.GetAddressBytes();
            return (b[0] & 0xfe) != 0xfc;   // fc00::/7 unique local
        }
        return false;
    }

    public void Dispose() => _http.Dispose();
}
