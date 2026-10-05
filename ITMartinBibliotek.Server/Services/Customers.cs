namespace ITMartinBibliotek.Server.Services;

// Who is looking at the app. Owner "" is the household (admin PIN); a customer
// sees and plays only the items whose Owner is their own key - their private CD
// copies must never mix with the household's, in either direction.
public sealed record Viewer(string Owner, string Name)
{
    public bool IsAdmin => Owner == "";
    public static readonly Viewer Admin = new("", "");
}

public sealed class Customer
{
    public string Key { get; set; } = "";        // "jytte" - stored as MediaItem.Owner
    public string Navn { get; set; } = "";       // shown in the app
    public string Pin { get; set; } = "";
    public string Bibliotek { get; set; } = "";  // the customer's own Jellyfin library (virtual folder name)

    // Shown on the customer's own "Sådan gør du" page (/kom-i-gang) - only to that customer.
    public string Upload { get; set; } = "";         // their upload box link incl. token
    public string JellyfinBruger { get; set; } = "";
    public string JellyfinKode { get; set; } = "";
}

// Customers come from config (Bibliotek__Kunder__0__Key / __Navn / __Pin / __Bibliotek),
// so adding one is an env line + restart, and PINs never sit in the database.
public sealed class Customers
{
    private readonly string _adminPin;
    private readonly List<Customer> _list;

    public Customers(IConfiguration cfg)
    {
        _adminPin = cfg["Bibliotek:AdminPin"] ?? "bibliotek2026";
        _list = (cfg.GetSection("Bibliotek:Kunder").Get<List<Customer>>() ?? [])
            .Where(c => c.Key.Trim() != "" && c.Pin.Trim().Length >= 4 && c.Bibliotek.Trim() != "" && c.Pin != _adminPin)
            .ToList();
    }

    public IReadOnlyList<Customer> All => _list;

    public Viewer? Resolve(string? pin)
    {
        if (string.IsNullOrEmpty(pin)) return null;
        if (pin == _adminPin) return Viewer.Admin;
        var c = _list.FirstOrDefault(c => c.Pin == pin);
        return c is null ? null : new Viewer(c.Key, c.Navn == "" ? c.Key : c.Navn);
    }

    public Customer? Get(string owner) => owner == "" ? null : _list.FirstOrDefault(c => c.Key == owner);

    public Viewer? ByOwner(string owner)
    {
        if (owner == "") return Viewer.Admin;
        var c = _list.FirstOrDefault(c => c.Key == owner);
        return c is null ? null : new Viewer(c.Key, c.Navn == "" ? c.Key : c.Navn);
    }

    // A Jellyfin item belongs to the customer whose library it sits in; everything else is the household's.
    public string OwnerForLibrary(string library) =>
        _list.FirstOrDefault(c => string.Equals(c.Bibliotek, library, StringComparison.OrdinalIgnoreCase))?.Key ?? "";
}
