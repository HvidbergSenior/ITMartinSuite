using ITMartinSalg.Domain;

namespace ITMartinSalg.Application;

/// <summary>The day offers (Salg's Tilbud page): Martin turns them on and off, changes the numbers, adds new ones, and
/// tries a basket against a day before the buyers see it.</summary>
public sealed class CampaignUseCases(ICampaignRepository campaigns, TimeProvider clock)
{
    public DateOnly Today => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

    public Task<List<Campaign>> AllAsync(CancellationToken ct = default) => campaigns.AllAsync(ct);

    public async Task<List<Campaign>> OnAsync(DateOnly day, CancellationToken ct = default) =>
        (await campaigns.AllAsync(ct)).Where(c => c.On(day)).ToList();

    /// <summary>Saves a new or changed offer. Refused when the numbers make no sense (a percentage of 0 or 100, a bundle
    /// of one thing, or a day that does not exist).</summary>
    public async Task SaveAsync(Campaign c, CancellationToken ct = default)
    {
        c.Name = c.Name.Trim();
        if (c.Name == "") throw new SalgException("Giv tilbuddet et navn, fx Bogmandag.");
        if (!CampaignKinds.All.Contains(c.Kind)) throw new SalgException("Vælg procent eller bundt.");
        if (c.ProductType != "" && !ProductTypes.All.Contains(c.ProductType)) throw new SalgException("Ukendt varetype.");
        if (c.Usage != "" && !Usages.All.Contains(c.Usage)) throw new SalgException("Vælg brugt, ny eller begge.");
        if (c.Weekday is < 0 or > 6 || c.DayOfMonth is < 1 or > 31) throw new SalgException("Ugedagen eller datoen findes ikke.");
        if (!c.Valid)
            throw new SalgException(c.Kind == CampaignKinds.Percent ? "Procent skal være mellem 1 og 99." : "Et bundt er mindst 2 ting til en pris over 0 kr.");
        await campaigns.SaveAsync(c, ct);
    }

    public async Task SetActiveAsync(Campaign c, bool active, CancellationToken ct = default)
    {
        c.Active = active;
        await campaigns.SaveAsync(c, ct);
    }

    public Task DeleteAsync(int id, CancellationToken ct = default) => campaigns.DeleteAsync(id, ct);

    /// <summary>"Prøv en kurv": what a basket of things at these prices would cost on a day.</summary>
    public async Task<PricedBasket> TryAsync(IReadOnlyList<BasketLine> basket, DateOnly day, CancellationToken ct = default) =>
        Campaigns.Price(basket, await campaigns.AllAsync(ct), day);
}
