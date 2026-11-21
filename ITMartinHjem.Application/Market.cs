using Microsoft.Extensions.DependencyInjection;

namespace ITMartinHjem.Application;

// The marketplace for customers (user 2026-10-24: "customers log in with kundenummer, see some products, mark items
// 'interessant' ... NO buying yet"; then a live auction picked by the marks). Kunder says whether the kundenummer works;
// Bogshoppen Salg keeps the marks. Both meet this site only as the shapes in the Contracts.

/// <summary>Kunder: may this kundenummer use the marketplace? (KunderRoles.Market)</summary>
public interface IMarketCodes
{
    /// <returns>Ok, or the reason in Danish - also when Kunder does not answer (nothing is let in unchecked).</returns>
    Task<(bool Ok, string Error)> CheckAsync(string code, CancellationToken ct = default);
}

/// <summary>Salg: the "interessant" marks.</summary>
public interface IInterestGateway
{
    Task<ShopInterest> ToggleAsync(string customer, int itemId, CancellationToken ct = default);
    Task<List<int>> MineAsync(string customer, CancellationToken ct = default);
}

/// <summary>Salg: Byd (hidden max) and Mit bundt (2026-11-05).</summary>
public interface IOfferGateway
{
    Task<ShopOfferAnswer> BidAsync(string customer, int itemId, int maxKr, CancellationToken ct = default);
    Task<Dictionary<int, int>> MyBidsAsync(string customer, CancellationToken ct = default);
    Task<ShopOfferAnswer> BundleAsync(string customer, IReadOnlyList<int> itemIds, int? kr, CancellationToken ct = default);
    // Haggling (2026-11-21)
    Task<ShopOfferAnswer> VerdictAsync(int itemId, ShopVerdictPlace place, CancellationToken ct = default);
    Task<List<ShopVerdict>> VerdictsAsync(IReadOnlyCollection<int> itemIds, CancellationToken ct = default);
    Task<ShopOfferAnswer> LoanAsync(ShopLoanPlace place, CancellationToken ct = default);
    Task<List<ShopLoanView>> MyLoansAsync(string customer, CancellationToken ct = default);
    Task<ShopOfferAnswer> CancelLoanAsync(string customer, int loanId, CancellationToken ct = default);
    Task<ShopOfferAnswer> ItemOfferAsync(string customer, int itemId, int kr, CancellationToken ct = default);
    Task<List<ShopBundleView>> MyBundlesAsync(string customer, CancellationToken ct = default);
    Task<ShopOfferAnswer> ReplyAsync(int bundleId, ShopBundleReply reply, CancellationToken ct = default);
}

/// <summary>"Mit bundt" while the customer picks (2026-11-05): kept here per kundenummer until they send it - nothing is
/// stored for good before "Send". Lost on a restart of the site, which only means picking again.</summary>
public sealed class BundleDrafts
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, List<int>> _d = new();
    public IReadOnlyList<int> Of(string customer) => _d.TryGetValue(customer, out var l) ? l.ToList() : [];
    public bool Has(string customer, int itemId) => _d.TryGetValue(customer, out var l) && l.Contains(itemId);
    public void Toggle(string customer, int itemId)
    {
        var l = _d.GetOrAdd(customer, _ => []);
        lock (l) { if (!l.Remove(itemId) && l.Count < 50) l.Add(itemId); }
    }
    public void Clear(string customer) => _d.TryRemove(customer, out _);
}

public sealed class MarketUseCases(IMarketCodes codes, IInterestGateway interests, IOfferGateway? offers = null)
{
    public const string Unreachable = "Det kan ikke gemmes lige nu – prøv igen om lidt.";

    /// <summary>"4821 7730" -> "48217730"; null when it is not 8 digits.</summary>
    public static string? Normalise(string? code)
    {
        var digits = new string((code ?? "").Where(char.IsDigit).ToArray());
        return digits.Length == 8 ? digits : null;
    }

    /// <returns>The kundenummer to remember, or the reason in Danish.</returns>
    public async Task<(string? Customer, string? Error)> LoginAsync(string? typed, CancellationToken ct = default)
    {
        if (Normalise(typed) is not { } code) return (null, "Dit nummer har 8 cifre – du finder det i mailen fra os.");
        var (ok, error) = await codes.CheckAsync(code, ct);
        return ok ? (code, null) : (null, error == "" ? "Det nummer virker ikke." : error);
    }

    public async Task<ShopInterest> ToggleAsync(string customer, int itemId, CancellationToken ct = default)
    {
        try { return await interests.ToggleAsync(customer, itemId, ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return new(false, 0, Unreachable); }
    }

    /// <summary>Byd: the most they would pay - Salg keeps it hidden until the Saturday auction.</summary>
    public async Task<ShopOfferAnswer> BidAsync(string customer, int itemId, int? maxKr, CancellationToken ct = default)
    {
        if (customer == "") return new(false, "Log ind med dit nummer først.");
        if (maxKr is not { } kr) return new(false, "Skriv, hvad du højst vil give.");
        try { return await (offers ?? throw new InvalidOperationException()).BidAsync(customer, itemId, kr, ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException) { return new(false, Unreachable); }
    }

    public async Task<Dictionary<int, int>> MyBidsAsync(string customer, CancellationToken ct = default)
    {
        if (customer == "" || offers is null) return [];
        try { return await offers.MyBidsAsync(customer, ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return []; }
    }

    /// <summary>Mit bundt sent: one bid for all (kr) or a question (null).</summary>
    public async Task<ShopOfferAnswer> BundleAsync(string customer, IReadOnlyList<int> itemIds, int? kr, CancellationToken ct = default)
    {
        if (customer == "") return new(false, "Log ind med dit nummer først.");
        if (itemIds.Count < 2) return new(false, "Læg mindst 2 ting i bundtet.");
        try { return await (offers ?? throw new InvalidOperationException()).BundleAsync(customer, itemIds, kr, ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException) { return new(false, Unreachable); }
    }

    // -- Hvad syntes du? (2026-11-21) --
    public async Task<ShopOfferAnswer> VerdictAsync(string customer, int itemId, string? name, string? verdict, string? teaser, CancellationToken ct = default)
    {
        if (customer == "") return new(false, "Log ind med dit nummer først.");
        try { return await (offers ?? throw new InvalidOperationException()).VerdictAsync(itemId, new ShopVerdictPlace(customer, name ?? "", verdict ?? "", teaser ?? ""), ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException) { return new(false, Unreachable); }
    }

    /// <summary>The approved verdicts for the things on a page - empty when Salg does not answer.</summary>
    public async Task<ILookup<int, ShopVerdict>> VerdictsAsync(IReadOnlyCollection<int> itemIds, CancellationToken ct = default)
    {
        if (itemIds.Count == 0 || offers is null) return Array.Empty<ShopVerdict>().ToLookup(v => v.ItemId);
        try { return (await offers.VerdictsAsync(itemIds, ct)).ToLookup(v => v.ItemId); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return Array.Empty<ShopVerdict>().ToLookup(v => v.ItemId); }
    }

    // -- Lån en konsol (2026-11-21) --
    public async Task<ShopOfferAnswer> LoanAsync(string customer, string console, DateTime friday, bool tenDvds, CancellationToken ct = default)
    {
        if (customer == "") return new(false, "Log ind med dit nummer først.");
        try { return await (offers ?? throw new InvalidOperationException()).LoanAsync(new ShopLoanPlace(customer, console, friday, tenDvds), ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException) { return new(false, Unreachable); }
    }

    public async Task<List<ShopLoanView>> MyLoansAsync(string customer, CancellationToken ct = default)
    {
        if (customer == "" || offers is null) return [];
        try { return await offers.MyLoansAsync(customer, ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return []; }
    }

    public async Task<ShopOfferAnswer> CancelLoanAsync(string customer, int loanId, CancellationToken ct = default)
    {
        if (customer == "") return new(false, "Log ind med dit nummer først.");
        try { return await (offers ?? throw new InvalidOperationException()).CancelLoanAsync(customer, loanId, ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException) { return new(false, Unreachable); }
    }

    /// <summary>"Dit bud" on one thing (2026-11-21) - haggled like a bundle.</summary>
    public async Task<ShopOfferAnswer> ItemOfferAsync(string customer, int itemId, int? kr, CancellationToken ct = default)
    {
        if (customer == "") return new(false, "Log ind med dit nummer først.");
        if (kr is not > 0) return new(false, "Skriv, hvad du vil give.");
        try { return await (offers ?? throw new InvalidOperationException()).ItemOfferAsync(customer, itemId, kr.Value, ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException) { return new(false, Unreachable); }
    }

    /// <summary>The customer's bundles (haggling, 2026-11-21) - empty when Salg does not answer.</summary>
    public async Task<List<ShopBundleView>> MyBundlesAsync(string customer, CancellationToken ct = default)
    {
        if (customer == "" || offers is null) return [];
        try { return await offers.MyBundlesAsync(customer, ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return []; }
    }

    /// <summary>Yes to the price, a new bid, or no thanks.</summary>
    public async Task<ShopOfferAnswer> ReplyAsync(string customer, int bundleId, bool accept, int? kr, bool reject, CancellationToken ct = default)
    {
        if (customer == "") return new(false, "Log ind med dit nummer først.");
        try { return await (offers ?? throw new InvalidOperationException()).ReplyAsync(bundleId, new ShopBundleReply(customer, accept, kr, reject), ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException) { return new(false, Unreachable); }
    }

    /// <summary>Empty when Salg does not answer - the page still works, the stars just start empty.</summary>
    public async Task<HashSet<int>> MineAsync(string customer, CancellationToken ct = default)
    {
        if (customer == "") return [];
        try { return (await interests.MineAsync(customer, ct)).ToHashSet(); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return []; }
    }
}

public static class MarketRegistration
{
    public static IServiceCollection AddHjemMarket(this IServiceCollection services)
    {
        services.AddScoped<MarketUseCases>();
        services.AddScoped<WishUseCases>();   // Mine ønsker (2026-11-19)
        return services;
    }
}
