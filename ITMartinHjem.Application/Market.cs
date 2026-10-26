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

public sealed class MarketUseCases(IMarketCodes codes, IInterestGateway interests)
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
        if (Normalise(typed) is not { } code) return (null, "Dit kundenummer har 8 cifre – du finder det i mailen fra os.");
        var (ok, error) = await codes.CheckAsync(code, ct);
        return ok ? (code, null) : (null, error == "" ? "Det kundenummer virker ikke." : error);
    }

    public async Task<ShopInterest> ToggleAsync(string customer, int itemId, CancellationToken ct = default)
    {
        try { return await interests.ToggleAsync(customer, itemId, ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return new(false, 0, Unreachable); }
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
        return services;
    }
}
