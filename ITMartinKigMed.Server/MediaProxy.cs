using ITMartinKigMed.Application;

namespace ITMartinKigMed.Server;

// ── Video: MediaMTX (container kigmed-media) behind this app, so one hostname serves everything.
// /media/... = WebRTC signalling (WHIP publish from the studio, WHEP play for viewers), /hls/... = the fallback.
// Publishing needs the studio cookie; the app adds MediaMTX's publish login itself.
public static class MediaProxy
{
    public static void AddMediaProxy(this IServiceCollection services) =>
        services.AddHttpClient("media", c => c.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

    public static void MapMediaProxy(this WebApplication app, Func<HttpContext, bool> isOwner)
    {
        var mediaWebRtc = app.Configuration["KigMed:MediaWebRtc"] ?? "http://kigmed-media:8889";
        var mediaHls = app.Configuration["KigMed:MediaHls"] ?? "http://kigmed-media:8888";
        var publishUser = app.Configuration["KigMed:PublishUser"] ?? "martin";
        var publishPass = app.Configuration["KigMed:PublishPass"] ?? "";

        async Task Proxy(HttpContext ctx, IHttpClientFactory http, string target, string prefix, bool publish, bool ownerOnly = true, bool keepQuery = false)
        {
            if (publish && ownerOnly && !isOwner(ctx)) { ctx.Response.StatusCode = 401; return; }
            // WHIP/WHEP need no query (and "t" is our guest token, not MediaMTX's business); LL-HLS does (_HLS_msn …)
            var req = new HttpRequestMessage(new HttpMethod(ctx.Request.Method), target + (keepQuery ? ctx.Request.QueryString.ToString() : ""));
            if (ctx.Request.ContentLength > 0 || ctx.Request.Headers.ContainsKey("Transfer-Encoding"))
            {
                req.Content = new StreamContent(ctx.Request.Body);
                if (ctx.Request.ContentType is { } ct) req.Content.Headers.TryAddWithoutValidation("Content-Type", ct);
            }
            foreach (var h in new[] { "If-Match", "Accept" })
                if (ctx.Request.Headers.TryGetValue(h, out var v)) req.Headers.TryAddWithoutValidation(h, v.ToArray());
            // MediaMTX's HLS sets its own cookies (cookieCheck, session) and redirects - send those back, never the studio login
            var mtxCookies = ctx.Request.Cookies.Where(c => c.Key != "kigmed_auth").Select(c => $"{c.Key}={c.Value}").ToList();
            if (mtxCookies.Count > 0) req.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", mtxCookies));
            if (publish && publishPass.Length > 0)
                req.Headers.Authorization = new("Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{publishUser}:{publishPass}")));
            try
            {
                using var res = await http.CreateClient("media").SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ctx.RequestAborted);
                ctx.Response.StatusCode = (int)res.StatusCode;
                foreach (var (k, v) in res.Headers.Concat(res.Content.Headers))
                {
                    if (k is "Transfer-Encoding" or "Connection" or "Server" or "Access-Control-Allow-Origin" or "Access-Control-Allow-Credentials") continue;
                    var values = v.ToArray();
                    // WHIP/WHEP answer with the session's own address - keep it on this host
                    if (k == "Location") values = values.Select(l => l.StartsWith('/') ? prefix + l : l).ToArray();
                    ctx.Response.Headers[k] = values;
                }
                ctx.Response.Headers.CacheControl = "no-store";
                await res.Content.CopyToAsync(ctx.Response.Body, ctx.RequestAborted);
            }
            catch (HttpRequestException)
            {
                ctx.Response.StatusCode = 502;
            }
        }

        app.Map("/media/{**path}", async (HttpContext ctx, IHttpClientFactory http, LiveHub hub, string path) =>
        {
            // Martin publishes to live/whip (studio cookie); WHIP's follow-up calls (PATCH, DELETE) go to live/whip/<session> too.
            if (path.StartsWith("live/whip", StringComparison.OrdinalIgnoreCase))
            {
                await Proxy(ctx, http, $"{mediaWebRtc}/{path}", "/media", publish: true);
                return;
            }
            // The viewer who has the floor publishes to gaest/whip with the one-time token Martin gave them.
            if (path.StartsWith("gaest/whip", StringComparison.OrdinalIgnoreCase))
            {
                var first = path.Equals("gaest/whip", StringComparison.OrdinalIgnoreCase);
                if (first && !hub.GuestTokenOk(ctx.Request.Query["t"])) { ctx.Response.StatusCode = 401; return; }
                await Proxy(ctx, http, $"{mediaWebRtc}/{path}", "/media", publish: true, ownerOnly: false);
                return;
            }
            await Proxy(ctx, http, $"{mediaWebRtc}/{path}", "/media", publish: false);   // WHEP play: open
        }).DisableAntiforgery();
        app.MapGet("/hls/{**path}", (HttpContext ctx, IHttpClientFactory http, string path) =>
            Proxy(ctx, http, $"{mediaHls}/{path}", "/hls", false, keepQuery: true));
    }
}
