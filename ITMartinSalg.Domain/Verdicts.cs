namespace ITMartinSalg.Domain;

/// <summary>"Hvad syntes du?" (user 2026-11-21: "people should be able to make small verdicts and teasers"): a customer's few
/// words about a thing and an optional spoiler-free teaser, signed with a first name only. Nothing is shown before Martin
/// says Godkendt (no self-service, no fake reviews, no stars).</summary>
public sealed class ItemVerdict
{
    public const int MaxVerdict = 80, MaxTeaser = 280, MaxName = 30;
    public const string New = "Ny", Approved = "Godkendt", Rejected = "Afvist";

    public int Id { get; set; }
    public int ItemId { get; set; }
    public string Customer { get; set; } = "";
    public string Name { get; set; } = "";
    public string Verdict { get; set; } = "";
    public string Teaser { get; set; } = "";
    public string Status { get; set; } = New;
    public DateTime CreatedAt { get; set; }
    public DateTime? DecidedAt { get; set; }

    public static ItemVerdict Write(string? customer, int itemId, string? name, string? verdict, string? teaser, DateTime now)
    {
        var v = Clean(verdict); var t = Clean(teaser); var n = Clean(name);
        if (itemId <= 0) throw new InvalidOperationException("Varen findes ikke.");
        if (v == "") throw new InvalidOperationException("Skriv, hvad du syntes – med få ord.");
        if (v.Length > MaxVerdict) throw new InvalidOperationException($"Din mening må højst være {MaxVerdict} tegn.");
        if (t.Length > MaxTeaser) throw new InvalidOperationException($"Teaseren må højst være {MaxTeaser} tegn.");
        if (n == "") throw new InvalidOperationException("Skriv dit fornavn.");
        if (n.Length > MaxName) n = n[..MaxName];
        // A first name only - never the whole name (it is shown to everybody).
        n = n.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        return new() { Customer = Kundenummer.Require(customer), ItemId = itemId, Name = n, Verdict = v, Teaser = t, CreatedAt = now };
    }

    public void Approve(DateTime now) => Decide(Approved, now);
    public void Reject(DateTime now) => Decide(Rejected, now);

    private void Decide(string to, DateTime now)
    {
        if (Status != New) throw new InvalidOperationException("Den er allerede afgjort.");
        (Status, DecidedAt) = (to, now);
    }

    private static string Clean(string? s) => string.Join(' ', (s ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
