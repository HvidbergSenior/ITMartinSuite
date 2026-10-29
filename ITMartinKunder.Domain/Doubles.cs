namespace ITMartinKunder.Domain;

// Sign-up checks (user 2026-10-29: "Should there be some validation when they signup?" -> names with capitals, and
// a flag for Martin when the same person seems to have signed up twice - fx Riksi with hotmaol.com and hotmail.com).

public static class CustomerNames
{
    // Small words that stay small inside a name ("Ole de Neergaard", "Anne van Dijk").
    private static readonly HashSet<string> Small = ["af", "de", "van", "von", "der", "den", "la", "le", "du", "da", "di"];

    /// <summary>"mette vibe sørensen" -> "Mette Vibe Sørensen", also after a hyphen ("anne-marie"). A word the person
    /// wrote with any capital is kept as written ("McDonald", "ITMartin").</summary>
    public static string Nice(string? name)
    {
        var words = (name ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words.Select((w, i) => w.Any(char.IsUpper) || (i > 0 && Small.Contains(w)) ? w
            : string.Join('-', w.Split('-').Select(p => p.Length == 0 ? p : char.ToUpperInvariant(p[0]) + p[1..]))));
    }
}

public static class Doubles
{
    /// <summary>Signed up within this long of each other with the same name - probably one person trying again.</summary>
    public static readonly TimeSpan Close = TimeSpan.FromDays(2);

    /// <summary>Per customer id: the other customer that may be the same person (newest pairs win). Same part before @
    /// with another domain, or the same name signed up close together.</summary>
    public static Dictionary<int, int> Find(IEnumerable<Customer> customers)
    {
        var all = customers.OrderBy(c => c.Id).ToList();
        var found = new Dictionary<int, int>();
        for (var i = 0; i < all.Count; i++)
            for (var j = i + 1; j < all.Count; j++)
            {
                var (a, b) = (all[i], all[j]);
                if (!Same(a, b)) continue;
                found[a.Id] = b.Id;
                found[b.Id] = a.Id;
            }
        return found;
    }

    private static bool Same(Customer a, Customer b)
    {
        var (la, lb) = (Local(a.Email), Local(b.Email));
        if (la.Length >= 4 && la == lb) return true;
        var (na, nb) = (a.Name.Trim().ToLowerInvariant(), b.Name.Trim().ToLowerInvariant());
        return na.Length > 0 && na == nb && (a.CreatedAt - b.CreatedAt).Duration() <= Close;
    }

    private static string Local(string email) => email.Split('@')[0].Trim().ToLowerInvariant();
}
