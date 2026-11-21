namespace ITMartinHjem.Domain;

/// <summary>Each shop's own invitation (user 2026-11-21: "not as a box on top but for each shop ... Kan du huske Videoaftner?
/// Kan du huske Gamingaftner? Skal vi lave en bogklub? Skal vi nørde tegneserier?"). Loan = the console loan goes with it;
/// Join = "Vil du være med?" to the chat (Martin makes the room with Claude - no self-service).</summary>
public sealed record ShopInvite(string Title, string Text, bool Loan, bool Join);

public static class ShopInvites
{
    public static readonly IReadOnlyDictionary<string, ShopInvite> BySlug = new Dictionary<string, ShopInvite>
    {
        ["film"] = new("Kan du huske videoaftener?",
            "Vælg selv filmene – 4 DVD'er for 50 kr eller 10 for 100 kr, og du beholder dem. Lån en konsol at se dem på.", Loan: true, Join: false),
        ["spil"] = new("Kan du huske gamingaftener?",
            "Lån en konsol fra fredag til mandag, og køb de spil, I vil spille – dem beholder I.", Loan: true, Join: false),
        ["boeger"] = new("Skal vi lave en bogklub?",
            "Vi læser den samme bog og mødes og snakker om den. Bøgerne finder du her.", Loan: false, Join: true),
        ["tegneserier"] = new("Skal vi nørde tegneserier?",
            "Album, gamle blade og serier, du ikke kan finde andre steder – kom og snak om dem med andre, der også elsker dem.", Loan: false, Join: true),
        ["musik"] = new("Elsker du cd og vinyler?",
            "Bladr i kasserne, find den plade, du ikke har hørt i 20 år – og snak musik med andre, der også samler.", Loan: false, Join: true),
    };

    public static ShopInvite? For(string? slug) => slug is not null && BySlug.TryGetValue(slug, out var i) ? i : null;
}
