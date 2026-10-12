using Microsoft.Extensions.DependencyInjection;

namespace ITMartinLager.Infrastructure.PriceSites;

// One price site as the use cases see it. The site classes keep their own way of asking (some by ISBN, some by
// title only) - this wraps them in the one shape.
internal sealed class PriceSource(
    string name, bool isNew, string country,
    Func<string, string, string, CancellationToken, Task<Listings?>> lookup,
    Func<string, string, string, string> searchUrl) : IPriceSource
{
    public string Name => name;
    public bool IsNew => isNew;
    public string Country => country;
    public Task<Listings?> LookupAsync(string title, string author, string isbn, CancellationToken ct) => lookup(title, author, isbn, ct);
    public string SearchUrl(string title, string author, string isbn) => searchUrl(title, author, isbn);
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

        void Add(Func<IServiceProvider, IPriceSource> make) => services.AddTransient(make);
        Add(sp => new PriceSource("DBA", false, "", (t, a, i, ct) => sp.GetRequiredService<DbaListings>().LookupAsync(t, a, ct),
            (t, a, i) => DbaListings.SearchUrl(Q(t, a))));
        Add(sp => Used(sp.GetRequiredService<GulogGratisListings>(), ""));
        Add(sp => new PriceSource("eBay", false, "", (t, a, i, ct) => sp.GetRequiredService<EbayListings>().LookupAsync(t, a, i, ct),
            (t, a, i) => sp.GetRequiredService<EbayListings>().SearchUrl(QOrIsbn(t, a, i))));
        Add(sp => new PriceSource("Tradera", false, "Sverige", (t, a, i, ct) => sp.GetRequiredService<TraderaListings>().LookupAsync(t, a, i, ct),
            (t, a, i) => TraderaListings.SearchUrl(QOrIsbn(t, a, i))));
        Add(sp => Used(sp.GetRequiredService<BlocketListings>(), "Sverige"));
        Add(sp => Used(sp.GetRequiredService<FinnListings>(), "Norge"));
        Add(sp => Used(sp.GetRequiredService<ToriListings>(), "Finland"));
        Add(sp => Used(sp.GetRequiredService<KleinanzeigenListings>(), "Tyskland"));
        Add(sp => new PriceSource("AbeBooks", false, "", (t, a, i, ct) => sp.GetRequiredService<AbeBooksListings>().LookupAsync(t, a, i, ct),
            (t, a, i) => AbeBooksListings.SearchUrl(QOrIsbn(t, a, i))));
        Add(sp => new PriceSource("ZVAB", false, "Tyskland", (t, a, i, ct) => sp.GetRequiredService<ZvabListings>().LookupAsync(t, a, i, ct),
            (t, a, i) => ZvabListings.SearchUrl(QOrIsbn(t, a, i))));
        Add(sp => new PriceSource("Bokbörsen", false, "Sverige", (t, a, i, ct) => sp.GetRequiredService<BokborsenListings>().LookupAsync(t, a, i, ct),
            (t, a, i) => BokborsenListings.SearchUrl(QOrIsbn(t, a, i))));
        Add(sp => new PriceSource("Nemos Bibliotek", false, "", (t, a, i, ct) => sp.GetRequiredService<NemosListings>().LookupAsync(t, a, ct),
            (t, a, i) => NemosListings.SearchUrl(t)));
        Add(sp => new PriceSource("Faraos", false, "", (t, a, i, ct) => sp.GetRequiredService<FaraosListings>().LookupAsync(t, a, ct),
            (t, a, i) => FaraosListings.SearchUrl(t)));
        // Amazon.de removed 2026-10-12 (user: "useless, too bad price") - it showed new prices far above what a used copy
        // sells for, and its one-at-a-time limit held every book check up.
        Add(sp => new PriceSource("Bog & idé", true, "", (t, a, i, ct) => sp.GetRequiredService<BogIdeListings>().LookupAsync(t, a, i, ct),
            (t, a, i) => BogIdeListings.SearchUrl(QOrIsbn(t, a, i))));
        return services;
    }

    private static PriceSource Used(UsedMarket m, string country) =>
        new(m.Name, false, country, (t, a, i, ct) => m.LookupAsync(t, a, ct), (t, a, i) => m.UrlFor(Q(t, a)));
}
