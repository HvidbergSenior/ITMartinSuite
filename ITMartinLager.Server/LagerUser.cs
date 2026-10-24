namespace ITMartinLager.Server;

/// <summary>Who uses Lager: Martin/Rico with the PIN (everything), or a helper with their kundenummer (user 2026-10-24):
/// boxes, Læs and correcting things - no prices, no delete, no Magic/Guld/Ordrer/auction, and no test-box placering.</summary>
public sealed record LagerUser(string Name, bool IsAdmin)
{
    /// <summary>The pages a helper may open (plus login, photos and the static files).</summary>
    public static bool HelperMayOpen(string path) =>
        path == "/" || path.StartsWith("/kasser") || path.StartsWith("/kasse/") || path.StartsWith("/vare/")
        || path.StartsWith("/varefoto/") || path.StartsWith("/kolofonfoto/") || path.StartsWith("/foto/") || path.StartsWith("/_blazor")
        || path.StartsWith("/api/logout");
}
