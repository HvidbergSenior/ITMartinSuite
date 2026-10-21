namespace ITMartinHjem.Domain;

// bogshoppen.dk is product-oriented (user 2026-10-12): what is coming, how to follow along, how to buy.
// Live auctions first; the products are shown soon. No votes - people sign up as customers instead (NO newsletters).
public static class BogshoppenRules
{
    public const string Source = "bogshoppen.dk: Liveauktioner";

    // Stored with the customer as proof of what they agreed to (markedsføringsloven §10, GDPR art. 7).
    public const string ConsentText = SignUpRules.ConsentText;

    // What the first live auctions will have - a title and one example each (user: "just the titles... give an example of each").
    public static readonly IReadOnlyList<(string Title, string Example)> Products =
    [
        ("Anders And", "Anders And & Co."),
        ("Tintin", "Tintin og Picaroerne"),
        ("Asterix", "Asterix og hans gæve gallere"),
        ("Lucky Luke", "Spøgelsesbyen"),
        ("Fantomet", "Fantomet Special"),
        ("Storm P.", "Pings Naturhistorie"),
        ("Superhelte fra DC og Marvel, på engelsk", "Spider-Geddon"),
        ("Jumbobøger", "Walt Disneys Mammutbog"),
        ("Bøger", "Night Watch af Terry Pratchett"),
        ("Musik på cd", "dansk rock og pop"),
        ("Film på dvd og Blu-ray", "tegnefilm og klassikere"),
    ];

    /// <summary>What the customer collects, kept as their interests (only titles from the list).</summary>
    public static string Interests(IEnumerable<string> picked, string? note)
    {
        var titles = Products.Select(p => p.Title).ToHashSet();
        var list = string.Join(", ", picked.Where(titles.Contains).Distinct());
        var n = (note ?? "").Trim();
        if (n.Length > 280) n = n[..280];
        return n.Length == 0 ? list : list.Length == 0 ? n : list + " | " + n;
    }
}
