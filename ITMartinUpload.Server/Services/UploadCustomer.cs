namespace ITMartinUpload.Server.Services;

/// <summary>
/// One customer we have handed an upload link to. Bound from configuration:
///   Customers__0__Slug=annette-kent
///   Customers__0__Name=Annette og Kent
///   Customers__0__Token=...           (optional, falls back to Upload:Token)
///   Customers__0__Greeting=...        (optional, overrides the default "Hej <Name>!")
///   Customers__0__Intro=...           (optional, the line under the greeting)
/// </summary>
public sealed class UploadCustomer
{
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string Token { get; set; } = "";
    public string Greeting { get; set; } = "";
    public string Intro { get; set; } = "";

    /// <summary>"Hej Annette og Kent!" unless the customer has its own greeting.</summary>
    public string GreetingOrDefault =>
        !string.IsNullOrWhiteSpace(Greeting) ? Greeting
        : !string.IsNullOrWhiteSpace(Name) ? $"Hej {Name}!"
        : "Velkommen!";
}

/// <summary>Everything the upload endpoint and the page need, in one place.</summary>
public sealed class UploadOptions
{
    public string Root { get; set; } = "/app/data";

    /// <summary>Shared fallback token, used when a customer has none of its own.</summary>
    public string Token { get; set; } = "";

    public List<UploadCustomer> Customers { get; set; } = [];

    public UploadCustomer? Find(string? slug) =>
        string.IsNullOrWhiteSpace(slug)
            ? null
            : Customers.FirstOrDefault(c =>
                string.Equals(c.Slug, slug, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The token a link for <paramref name="slug"/> must carry. Empty means "no token required".
    /// A customer-specific token wins, so one customer's link never opens another's folder.
    /// </summary>
    public string ExpectedToken(string? slug)
    {
        var customer = Find(slug);
        return !string.IsNullOrEmpty(customer?.Token) ? customer!.Token : Token;
    }

    public bool IsAuthorized(string? slug, string? token)
    {
        var expected = ExpectedToken(slug);
        return string.IsNullOrEmpty(expected) || string.Equals(expected, token, StringComparison.Ordinal);
    }
}
