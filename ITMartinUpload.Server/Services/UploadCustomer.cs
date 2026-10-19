namespace ITMartinUpload.Server.Services;

/// <summary>
/// One customer we have handed an upload link to. Bound from configuration:
///   Customers__0__Slug=annette-kent
///   Customers__0__Name=Annette og Kent
///   Customers__0__Token=...           (optional, falls back to Upload:Token)
///   Customers__0__Greeting=...        (optional, overrides the default "Hej <Name>!")
///   Customers__0__Intro=...           (optional, the line under the greeting)
///   Customers__0__Music=true          (optional, CD rips: keeps Artist/Album folders)
/// </summary>
public sealed class UploadCustomer
{
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string Token { get; set; } = "";
    public string Greeting { get; set; } = "";
    public string Intro { get; set; } = "";

    /// <summary>Customers__N__GalleryUrl (+ optional __GalleryNote): a "Se jeres galleri" box at the top of the page,
    /// once FileSorter has made a gallery from what they sent (Annette og Kent 2026-10-16).</summary>
    public string GalleryUrl { get; set; } = "";
    public string GalleryNote { get; set; } = "";

    /// <summary>A music customer (Customers__N__Music=true): files arrive as Artist/Album/track from
    /// Rip CD, so the folders are kept - flat, every album's "01 …" and folder.jpg would collide.</summary>
    public bool Music { get; set; }

    /// <summary>Customers__N__Library: the customer's FileSorter library folder under Upload:LibraryRoot, where
    /// _kilder.json says what happened to each uploaded file. Empty = same as the slug.</summary>
    public string Library { get; set; } = "";

    /// <summary>
    /// An open event (Customers__N__Public=true): the link works without a code, for anyone
    /// who has it. Only this folder is opened - every other customer still needs its token.
    /// </summary>
    public bool Public { get; set; }

    /// <summary>Earlier slugs of a renamed event (Customers__N__OldSlugs=messe1,…): links and
    /// home-screen icons made before the rename are forwarded instead of refused.</summary>
    public string OldSlugs { get; set; } = "";

    public bool HadSlug(string slug) =>
        OldSlugs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(slug, StringComparer.OrdinalIgnoreCase);

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

    /// <summary>FileSorter's library folders, mounted read-only (status overview per customer).</summary>
    public string LibraryRoot { get; set; } = "/app/library";

    /// <summary>Shared fallback token, used when a customer has none of its own.</summary>
    public string Token { get; set; } = "";

    public List<UploadCustomer> Customers { get; set; } = [];

    /// <summary>The customer that used to be called <paramref name="slug"/>, if it was renamed.</summary>
    public UploadCustomer? Renamed(string? slug) =>
        string.IsNullOrWhiteSpace(slug) || Find(slug) is not null ? null : Customers.FirstOrDefault(c => c.HadSlug(slug));

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
        if (Find(slug)?.Public == true) return true;
        var expected = ExpectedToken(slug);
        return string.IsNullOrEmpty(expected) || string.Equals(expected, token, StringComparison.Ordinal);
    }
}
