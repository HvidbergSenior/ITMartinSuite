using Microsoft.Extensions.DependencyInjection;

namespace ITMartinLager.Infrastructure.PriceSites;

// One price site as the use cases see it. The site classes keep their own way of asking (some by ISBN, some by
// title only) - this wraps them in the one shape.
internal sealed class PriceSource(
    string name, bool isNew, string country,
    Func<string, string, string, CancellationToken, Task<Listings?>> lookup,
    Func<string, string, string, string> searchUrl,
    string[]? kinds = null) : IPriceSource
{
    public string Name => name;
    public bool IsNew => isNew;
    public string Country => country;
    public Task<Listings?> LookupAsync(string title, string author, string isbn, CancellationToken ct) => lookup(title, author, isbn, ct);
    public string SearchUrl(string title, string author, string isbn) => searchUrl(title, author, isbn);
    public bool Fits(string kind) => kinds is null || kinds.Contains(kind);
}

internal static class PriceSourceRegistration
{
    private static string Q(string title, string author) => Pricing.QueryFor(title, author);
    private static string QOrIsbn(string title, string author, string isbn) => isbn != "" ? isbn : Q(title, author);

    // Every site gets its own typed HttpClient (gzip on - Amazon answers gzip even when not asked), then joins the
    // list in the order the item page shows them: used markets first, new shops last.
    public static IServiceCollection AddPriceSources(this IServiceCollection services)
    {
        static HttpMessageHandler Gunzip() => new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.All };
        void Client<T>() where T : class =>
            services.AddHttpClient<T>(c => c.Timeout = TimeSpan.FromSeconds(15)).ConfigurePrimaryHttpMessageHandler(Gunzip);
        Client<DbaListings>(); Client<GulogGratisListings>(); Client<EbayListings>(); Client<TraderaListings>(); Client<BlocketListings>();
        Client<FinnListings>(); Client<ToriListings>(); Client<KleinanzeigenListings>(); Client<AbeBooksListings>(); Client<ZvabListings>();
        Client<BokborsenListings>(); Client<NemosListings>(); Client<FaraosListings>(); Client<BogIdeListings>();
        Client<DiscogsListings>();
        string[] paper = Pricing.PaperKinds;

        void Add(Func<IServiceProvider, IPriceSource> make) => services.AddTransient(make);
        services.AddTransient<IDearestListings>(sp => sp.GetRequiredService<DbaListings>());
        Add(sp => new PriceSource("DBA", false, "", (t, a, i, ct) => sp.GetRequiredService<DbaListings>().LookupAsync(t, a, ct),
            (t, a, i) => DbaListings.SearchUrl(Q(t, a))));
        Add(sp => Used(sp.GetRequiredService<GulogGratisListings>(), ""));
        Add(sp => new PriceSource("eBay", false, "", (t, a, i, ct) => sp.GetRequiredService<EbayListings>().LookupAsync(t, a, i, ct),
            (t, a, i) => sp.GetRequiredService<EbayListings>().SearchUrl(QOrIsbn(t, a, i))));
        // American comics (superhero comics above all) and merchandise sell in dollars - eBay's US site.
        Add(sp => new PriceSource("eBay USA", false, "USA", (t, a, i, ct) => sp.GetRequiredService<EbayListings>().LookupAsync(t, a, i, ct, "EBAY_US"),
            (t, a, i) => sp.GetRequiredService<EbayListings>().SearchUrl(QOrIsbn(t, a, i), "EBAY_US"), [Kinds.Tegneserie, Kinds.Superhelte, Kinds.Merch]));
        // HipComic tried 2026-10-12: its Cloudflare check answers 403 to every .NET client (curl gets in) - left out.
        // CDs, records and tapes: Discogs, the record collectors' market - copies for sale and the lowest price, in kroner.
        // One source per format, so a record is searched as vinyl and a tape as a cassette - only the one that fits runs.
        foreach (var (kind, format) in new[] { (Kinds.Cd, DiscogsListings.Cd), (Kinds.Vinyl, DiscogsListings.Vinyl), (Kinds.Kassette, DiscogsListings.Cassette) })
            Add(sp => new PriceSource("Discogs", false, "", (t, a, i, ct) => sp.GetRequiredService<DiscogsListings>().LookupAsync(t, a, ct, format),
                (t, a, i) => DiscogsListings.SearchUrl(t, a, format), [kind]));
        Add(sp => new PriceSource("Tradera", false, "Sverige", (t, a, i, ct) => sp.GetRequiredService<TraderaListings>().LookupAsync(t, a, i, ct),
            (t, a, i) => TraderaListings.SearchUrl(QOrIsbn(t, a, i))));
        Add(sp => Used(sp.GetRequiredService<BlocketListings>(), "Sverige"));
        Add(sp => Used(sp.GetRequiredService<FinnListings>(), "Norge"));
        Add(sp => Used(sp.GetRequiredService<ToriListings>(), "Finland"));
        Add(sp => Used(sp.GetRequiredService<KleinanzeigenListings>(), "Tyskland"));
        Add(sp => new PriceSource("AbeBooks", false, "", (t, a, i, ct) => sp.GetRequiredService<AbeBooksListings>().LookupAsync(t, a, i, ct),
            (t, a, i) => AbeBooksListings.SearchUrl(QOrIsbn(t, a, i)), Pricing.AntiquarianKinds));
        Add(sp => new PriceSource("ZVAB", false, "Tyskland", (t, a, i, ct) => sp.GetRequiredService<ZvabListings>().LookupAsync(t, a, i, ct),
            (t, a, i) => ZvabListings.SearchUrl(QOrIsbn(t, a, i)), Pricing.AntiquarianKinds));
        Add(sp => new PriceSource("Bokbörsen", false, "Sverige", (t, a, i, ct) => sp.GetRequiredService<BokborsenListings>().LookupAsync(t, a, i, ct),
            (t, a, i) => BokborsenListings.SearchUrl(QOrIsbn(t, a, i)), Pricing.AntiquarianKinds));
        Add(sp => new PriceSource("Nemos Bibliotek", false, "", (t, a, i, ct) => sp.GetRequiredService<NemosListings>().LookupAsync(t, a, ct),
            (t, a, i) => NemosListings.SearchUrl(t), paper));
        Add(sp => new PriceSource("Faraos", false, "", (t, a, i, ct) => sp.GetRequiredService<FaraosListings>().LookupAsync(t, a, ct),
            (t, a, i) => FaraosListings.SearchUrl(t), paper));
        // Amazon.de removed 2026-10-12 (user: "useless, too bad price") - it showed new prices far above what a used copy
        // sells for, and its one-at-a-time limit held every book check up.
        Add(sp => new PriceSource("Bog & idé", true, "", (t, a, i, ct) => sp.GetRequiredService<BogIdeListings>().LookupAsync(t, a, i, ct),
            (t, a, i) => BogIdeListings.SearchUrl(QOrIsbn(t, a, i)), paper));
        return services;
    }

    private static PriceSource Used(UsedMarket m, string country) =>
        new(m.Name, false, country, (t, a, i, ct) => m.LookupAsync(t, a, ct), (t, a, i) => m.UrlFor(Q(t, a)));
}
