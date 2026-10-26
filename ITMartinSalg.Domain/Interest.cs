namespace ITMartinSalg.Domain;

/// <summary>"Interessant" on the marketplace (user 2026-10-24: customers log in with their kundenummer, see the things,
/// mark them "interessant" - NO buying yet; the live auction later picks its items by how many marked them). One mark
/// per customer per thing; pressing again takes it back.</summary>
public sealed class Interest
{
    public int Id { get; set; }
    public int ItemId { get; set; }              // the item in Lager
    public string Customer { get; set; } = "";   // the kundenummer (8 digits) - Kunder knows who it is
    public DateTime MarkedAt { get; set; }

    /// <summary>A new mark. Throws when the kundenummer is not 8 digits.</summary>
    public static Interest Mark(string? customer, int itemId, DateTime now) =>
        new() { Customer = Kundenummer.Require(customer), ItemId = itemId, MarkedAt = now };
}

/// <summary>The kundenummer from Kunder: 8 digits, typed by hand (spaces and dashes do not matter). Salg only stores it;
/// whether it works is asked in Kunder by the site the customer logs into.</summary>
public static class Kundenummer
{
    public static string? Normalise(string? code)
    {
        var digits = new string((code ?? "").Where(char.IsDigit).ToArray());
        return digits.Length == 8 ? digits : null;
    }

    public static string Require(string? code) =>
        Normalise(code) ?? throw new InvalidOperationException("Dit kundenummer har 8 cifre – du finder det i mailen fra os.");
}
