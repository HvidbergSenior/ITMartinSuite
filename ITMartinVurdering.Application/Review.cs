using ITMartinVarer.Contracts;
using ITMartinVurdering.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinVurdering.Application;

/// <summary>Port: Lager owns the things and hunts the prices; Vurdering only asks and answers. Implemented in Infrastructure.</summary>
public interface ILagerReview
{
    Task<VurderingQueue?> QueueAsync(string pile, int take, IReadOnlyCollection<int> skip, string usage, CancellationToken ct);
    /// <returns>Null when saved, else Lager's (Danish) reason.</returns>
    Task<string?> SetUsageAsync(int itemId, string usage, CancellationToken ct);
    /// <returns>Null when saved, else Lager's (Danish) reason.</returns>
    Task<string?> SetPriceAsync(int itemId, int kr, CancellationToken ct);
    Task<byte[]?> PhotoAsync(int itemId, CancellationToken ct);
}

public sealed record ReviewResult(bool Ok, string Message);

/// <summary>Step 2 of Bogshoppen: one thing at a time - Ok (the suggestion) or the person's own price.</summary>
public sealed class ReviewUseCases(ILagerReview lager)
{
    public Task<VurderingQueue?> NextAsync(string? pile, IReadOnlyCollection<int> skip, CancellationToken ct = default, string? usage = null) =>
        lager.QueueAsync(VurderingPiles.Valid(pile) ? pile! : VurderingPiles.Assess, 1, skip, VurderingFilter.Valid(usage), ct);

    /// <summary>"Som ny" / "Brugt" on the thing shown.</summary>
    public async Task<ReviewResult> UsageAsync(int itemId, string? usage, CancellationToken ct = default)
    {
        var u = Usages.Normalise(usage);
        if (u == Usages.New) return new(false, "Ny sættes i Lager.");
        return await lager.SetUsageAsync(itemId, u, ct) is { } error ? new(false, error) : new(true, $"Gemt: {u.ToLowerInvariant()}.");
    }

    public async Task<ReviewResult> OkAsync(int itemId, int? hintKr, CancellationToken ct = default) =>
        PriceRules.Ok(hintKr) is { } kr ? await SaveAsync(itemId, kr, ct) : new(false, "Der er ikke noget forslag – skriv selv en pris.");

    public async Task<ReviewResult> OwnAsync(int itemId, string? typed, CancellationToken ct = default) =>
        PriceRules.Parse(typed) is { } kr ? await SaveAsync(itemId, kr, ct)
        : new(false, $"Skriv en pris i hele kroner, mellem {PriceRules.Min} og {PriceRules.Max:N0} kr.");

    private async Task<ReviewResult> SaveAsync(int itemId, int kr, CancellationToken ct) =>
        await lager.SetPriceAsync(itemId, kr, ct) is { } error ? new(false, error) : new(true, $"Gemt: {kr} kr.");

    public Task<byte[]?> PhotoAsync(int itemId, CancellationToken ct = default) => lager.PhotoAsync(itemId, ct);
}

public static class VurderingApplication
{
    public static IServiceCollection AddVurderingApplication(this IServiceCollection services) => services.AddScoped<ReviewUseCases>();
}
