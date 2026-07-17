namespace ITMartinR6Assistant.Server;

// Set once when the process starts (not per-request) - used as a cache-busting
// query string on static assets so a fresh deploy always invalidates
// Cloudflare's edge cache instead of waiting out its max-age.
public static class BuildInfo
{
    public static readonly string Version = DateTime.UtcNow.Ticks.ToString();
}
