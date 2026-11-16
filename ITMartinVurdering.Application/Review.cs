using ITMartinVarer.Contracts;
using ITMartinVurdering.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinVurdering.Application;

/// <summary>Port: Lager owns the things and hunts the prices; Vurdering only asks and answers. Implemented in Infrastructure.</summary>
public interface ILagerReview
{
    Task<VurderingQueue?> QueueAsync(string pile, int take, IReadOnlyCollection<int> skip, string usage, CancellationToken ct);
    /// <summary>One thing by its number or words (2026-11-16 Søg). Null = Lager did not answer.</summary>
    Task<VurderingQueue?> SearchAsync(string text, IReadOnlyCollection<int> skip, CancellationToken ct);
    /// <returns>Null when saved, else Lager's (Danish) reason.</returns>
    Task<string?> SetUsageAsync(int itemId, string usage, CancellationToken ct);
    /// <returns>Null when saved, else Lager's (Danish) reason.</returns>
    Task<string?> SetPriceAsync(int itemId, int kr, CancellationToken ct);
    Task<byte[]?> PhotoAsync(int itemId, CancellationToken ct);
    /// <returns>Null when saved, else Lager's (Danish) reason.</returns>
    Task<string?> SetRareAsync(int itemId, bool rare, CancellationToken ct);
    /// <returns>Null when saved, else Lager's (Danish) reason.</returns>
    Task<string?> SetNoteAsync(int itemId, string note, CancellationToken ct);
    /// <returns>Null when the photo was cut, else Lager's (Danish) reason.</returns>
    Task<string?> CropOwnAsync(int itemId, PhotoArea keep, CancellationToken ct);
    /// <returns>Null when the photo was turned, else Lager's (Danish) reason.</returns>
    Task<string?> RotateAsync(int itemId, int degrees, CancellationToken ct);
    /// <summary>Kassér: the status the thing had (for Fortryd), or null when Lager did not do it.</summary>
    Task<string?> DiscardAsync(int itemId, CancellationToken ct);
    /// <returns>Null when the thing is back, else Lager's (Danish) reason.</returns>
    Task<string?> UndoDiscardAsync(int itemId, string was, CancellationToken ct);
    /// <returns>Null when the cut-out was made, else Lager's (Danish) reason.</returns>
    Task<string?> RecutAsync(int itemId, PhotoArea area, CancellationToken ct);
    Task<byte[]?> PileAsync(int itemId, CancellationToken ct);
    /// <returns>Null when saved, else Lager's (Danish) reason.</returns>
    Task<string?> SetPhotoHiddenAsync(int itemId, bool hidden, CancellationToken ct);
    /// <returns>Null when the mark was painted over, else Lager's (Danish) reason.</returns>
    Task<string?> CoverMarkAsync(int itemId, PhotoArea area, CancellationToken ct);
    /// <returns>Null when the original photo is back, else Lager's (Danish) reason.</returns>
    Task<string?> UncoverAsync(int itemId, CancellationToken ct);
    /// <summary>Lager asks every price site again for this thing. Sites that answered; null = Lager did not answer.</summary>
    Task<int?> FindPricesAsync(int itemId, CancellationToken ct);
    /// <summary>Ting/Strik things waiting for a description (2026-11-10). Null = Lager did not answer.</summary>
    Task<List<VurderingDescribe>?> WaitingDescriptionsAsync(int take, CancellationToken ct);
    /// <returns>True when Lager kept it.</returns>
    Task<bool> SetDescriptionAsync(int itemId, VurderingDescription d, CancellationToken ct);
}

public sealed record ReviewResult(bool Ok, string Message);

/// <summary>Step 2 of Bogshoppen: one thing at a time - Ok (the suggestion) or the person's own price.</summary>
public sealed class ReviewUseCases(ILagerReview lager)
{
    public Task<VurderingQueue?> NextAsync(string? pile, IReadOnlyCollection<int> skip, CancellationToken ct = default, string? usage = null) =>
        lager.QueueAsync(VurderingPiles.Valid(pile) ? pile! : VurderingPiles.Assess, 1, skip, VurderingFilter.Valid(usage), ct);

    /// <summary>Søg (user 2026-11-16: "not easy to search for"): a number opens that thing, words the matching ones in turn.</summary>
    public Task<VurderingQueue?> SearchAsync(string? text, IReadOnlyCollection<int> skip, CancellationToken ct = default) =>
        lager.SearchAsync((text ?? "").Trim(), skip, ct);

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

    /// <summary>"Sjælden – vurderes manuelt" on or off (2026-11-16). Rare = out of the 3 kr bin, waits here for a person's price.</summary>
    public async Task<ReviewResult> RareAsync(int itemId, bool rare, CancellationToken ct = default) =>
        await lager.SetRareAsync(itemId, rare, ct) is { } error ? new(false, error)
        : new(true, rare ? "Markeret som sjælden." : "Ikke sjælden længere – den går tilbage til sin normale pris.");

    /// <summary>The person's comment on the thing.</summary>
    public async Task<ReviewResult> NoteAsync(int itemId, string? note, CancellationToken ct = default) =>
        await lager.SetNoteAsync(itemId, (note ?? "").Trim(), ct) is { } error ? new(false, error) : new(true, "Kommentaren er gemt.");

    /// <summary>Beskær (user 2026-11-16: "also some way to cut things off pictures").</summary>
    public async Task<ReviewResult> CropOwnAsync(int itemId, PhotoArea? keep, CancellationToken ct = default) =>
        keep is null ? new(false, "Træk en firkant rundt om det, der skal blive.")
        : await lager.CropOwnAsync(itemId, keep, ct) is { } error ? new(false, error) : new(true, "Billedet er beskåret. »Fortryd« giver det gamle tilbage.");

    /// <summary>Drej (user 2026-11-16: "add a way to rotate pictures" + "could be manually").</summary>
    public async Task<ReviewResult> RotateAsync(int itemId, int degrees, CancellationToken ct = default) =>
        await lager.RotateAsync(itemId, degrees, ct) is { } error ? new(false, error) : new(true, "Billedet er drejet.");

    /// <summary>Kassér (user 2026-11-16: "so i should be able to say discard"): a bad reading leaves the stock, kept in Lager as
    /// Ikke købt. Returns the status it had, so Fortryd can bring it back; null = it did not happen.</summary>
    public Task<string?> DiscardAsync(int itemId, CancellationToken ct = default) => lager.DiscardAsync(itemId, ct);

    public async Task<ReviewResult> UndoDiscardAsync(int itemId, string? was, CancellationToken ct = default) =>
        string.IsNullOrWhiteSpace(was) ? new(false, "Der er intet at fortryde.")
        : await lager.UndoDiscardAsync(itemId, was, ct) is { } error ? new(false, error) : new(true, "Den er tilbage på lager.");

    /// <summary>Ret udsnittet (2026-11-16, "the picture is wrong"): a new cut-out from the pile photo the thing was read from.</summary>
    public async Task<ReviewResult> RecutAsync(int itemId, PhotoArea? area, CancellationToken ct = default) =>
        area is null ? new(false, "Træk en firkant rundt om hele tingen først.")
        : await lager.RecutAsync(itemId, area, ct) is { } error ? new(false, error) : new(true, "Nyt billede lavet. »Fortryd« giver det gamle tilbage.");

    public Task<byte[]?> PileAsync(int itemId, CancellationToken ct = default) => lager.PileAsync(itemId, ct);

    /// <summary>A bad photo off bogshoppen.dk, or back on (2026-11-16: "I want a way to not show the bad pictures").</summary>
    public async Task<ReviewResult> PhotoHiddenAsync(int itemId, bool hidden, CancellationToken ct = default) =>
        await lager.SetPhotoHiddenAsync(itemId, hidden, ct) is { } error ? new(false, error)
        : new(true, hidden ? "Billedet vises ikke på bogshoppen.dk – tag et nyt i Lager, når du har tid." : "Billedet vises igen på bogshoppen.dk.");

    /// <summary>Paint over the price mark the person dragged a box around (2026-11-16: "The mark should be gone from pictures").</summary>
    public async Task<ReviewResult> CoverMarkAsync(int itemId, PhotoArea? area, CancellationToken ct = default) =>
        area is null ? new(false, "Træk en firkant hen over mærket først.")
        : await lager.CoverMarkAsync(itemId, area, ct) is { } error ? new(false, error) : new(true, "Mærket er malet over.");

    public async Task<ReviewResult> UncoverAsync(int itemId, CancellationToken ct = default) =>
        await lager.UncoverAsync(itemId, ct) is { } error ? new(false, error) : new(true, "Billedet er som før.");

    /// <summary>"Find priser nu" (2026-11-15, moved from Lager's Vare page): the prices are found again, then the thing is shown anew.</summary>
    public async Task<ReviewResult> FindPricesAsync(int itemId, CancellationToken ct = default) =>
        await lager.FindPricesAsync(itemId, ct) is { } sites
            ? new(true, sites == 0 ? "Ingen sider svarede – prøv igen om lidt." : $"Spurgt {sites} sider – priserne er opdateret.")
            : new(false, "Lager svarer ikke lige nu – prøv igen om lidt.");
}

public static class VurderingApplication
{
    public static IServiceCollection AddVurderingApplication(this IServiceCollection services) => services.AddScoped<ReviewUseCases>()
        .AddScoped<PriceListUseCases>()
        .AddScoped<DescribeUseCases>()
        .AddSingleton(sp => new DodsboUseCases(sp.GetRequiredService<ISagStore>(), sp.GetRequiredService<IThingValuer>(), () => DateTime.Now));
}
