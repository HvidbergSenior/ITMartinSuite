using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ITMartin.Shared.UI.Kolibri;

public static class KolibriExtensions
{
    /// <summary>Registers the app's Kolibri identity. Call once in Program.cs.</summary>
    public static IServiceCollection AddKolibri(this IServiceCollection services, Action<KolibriAppInfo> configure)
    {
        var info = new KolibriAppInfo();
        configure(info);
        services.AddSingleton(info);
        return services;
    }

    /// <summary>
    /// Maps the standard Kolibri endpoints:
    ///   GET  /health              -> json for Kontrol (status, version, uptime, checks)
    ///   POST /api/hjaelp          -> "Noget virker ikke" report, logged with the KOLIBRI-HJAELP marker
    ///   GET  /manifest.webmanifest -> PWA manifest built from KolibriAppInfo
    ///   GET  /kolibri-sw.js       -> minimal service worker (installability only, no offline cache)
    /// </summary>
    public static IEndpointRouteBuilder MapKolibri(this IEndpointRouteBuilder app)
    {
        var started = DateTime.UtcNow;

        app.MapGet("/health", async (KolibriAppInfo info, IServiceProvider sp) =>
        {
            var items = new List<KolibriHealthItem> { new("app", true, info.Version) };
            if (info.HealthChecks is not null)
            {
                try { items.AddRange(await info.HealthChecks(sp)); }
                catch (Exception ex) { items.Add(new("checks", false, ex.Message)); }
            }
            var ok = items.All(i => i.Ok);
            var body = new
            {
                status = ok ? "ok" : "fail",
                app = info.Name,
                kolibri = info.KolibriName,
                tenant = info.Tenant,
                version = info.Version,
                uptimeSeconds = (int)(DateTime.UtcNow - started).TotalSeconds,
                checks = items,
            };
            return Results.Json(body, statusCode: ok ? 200 : 503);
        }).AllowAnonymous();

        app.MapPost("/api/hjaelp", async (HttpContext ctx, KolibriAppInfo info, ILoggerFactory lf) =>
        {
            var log = lf.CreateLogger("Kolibri.Hjaelp");
            var form = await ctx.Request.ReadFormAsync();
            string F(string k) => (form[k].ToString() ?? "").Trim();
            var report = new
            {
                app = info.Name, tenant = info.Tenant, version = info.Version,
                page = F("page"), text = F("text"), name = F("name"), contact = F("contact"),
                ua = ctx.Request.Headers.UserAgent.ToString(), at = DateTime.UtcNow,
            };
            // One line, one marker: Kontrol greps container logs for KOLIBRI-HJAELP.
            log.LogWarning("KOLIBRI-HJAELP {Report}", JsonSerializer.Serialize(report));
            return Results.Ok(new { ok = true });
        }).AllowAnonymous();

        app.MapGet("/manifest.webmanifest", (KolibriAppInfo info) =>
        {
            var manifest = new
            {
                name = info.DisplayTitle,
                short_name = info.Name,
                description = info.Tagline,
                start_url = "/",
                scope = "/",
                display = "standalone",
                background_color = info.ThemeColor,
                theme_color = info.ThemeColor,
                lang = "da",
                icons = new[]
                {
                    new { src = "/" + info.IconPath.TrimStart('/'), sizes = "any", type = "image/svg+xml", purpose = "any" },
                    new { src = "/" + info.IconPath.TrimStart('/'), sizes = "any", type = "image/svg+xml", purpose = "maskable" },
                },
            };
            return Results.Json(manifest, contentType: "application/manifest+json");
        }).AllowAnonymous();

        app.MapGet("/kolibri-sw.js", () => Results.Text(
            "self.addEventListener('install',()=>self.skipWaiting());self.addEventListener('activate',e=>e.waitUntil(self.clients.claim()));self.addEventListener('fetch',()=>{});",
            "application/javascript")).AllowAnonymous();

        return app;
    }
}
