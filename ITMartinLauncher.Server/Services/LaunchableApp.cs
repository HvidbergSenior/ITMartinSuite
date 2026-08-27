namespace ITMartinLauncher.Server.Services;

// Config for one on-demand app - hardcoded here rather than a database since
// this is a short, rarely-changing list (see CLAUDE.md "minimal and simple").
// Add a new entry when a new app should default to off.
public sealed record LaunchableApp(
    string Name,
    string ContainerName,
    string HealthCheckUrl,
    string OpenUrl,
    string Emoji);

public static class LaunchableApps
{
    public static readonly IReadOnlyList<LaunchableApp> All =
    [
        new LaunchableApp(
            "Star Realms",
            "star-realms-web",
            "http://star-realms-web:8080/",
            "https://starrealms.itmartin.dk",
            "🃏"),
    ];
}
