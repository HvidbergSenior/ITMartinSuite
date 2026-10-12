namespace ITMartinLager.Domain;

public sealed record MagicOwner(string Owner, int Cards, int Copies, decimal Eur);
public sealed record MagicCardInfo(Guid Id, string Name, string SetCode, string SetName, int? ReleaseYear, string CollectorNumber,
    int Quantity, decimal? EurPrice, decimal? UsdPrice, string? ScryfallId);

// Magic cards from the MTG Scanner as items in the register.
public static class MagicCards
{
    // The krone is pegged to the euro; Scryfall's EUR price is Cardmarket's trend price.
    public const decimal EurToDkk = 7.46m;

    public static decimal? Kr(decimal? eur) => eur is { } e && e > 0 ? Math.Max(1, Math.Round(e * EurToDkk)) : null;

    private static string Interest(MagicCardInfo c, decimal? kr) =>
        $"Magic: The Gathering – {c.Name} fra {c.SetName}" + (c.ReleaseYear is { } y ? $" ({y})" : "") + ", nr. " + c.CollectorNumber
        + (kr is { } k ? $". Cardmarket-pris ca. {k:0} kr pr. kort (Scryfall)." : ".");

    // Fetching again updates the same row (quantity, price hint). A sold item is left alone. False = nothing changed.
    public static bool Update(Item item, MagicCardInfo c)
    {
        if (item.Status == Statuses.Solgt) return false;
        var kr = Kr(c.EurPrice);
        item.Quantity = Math.Max(1, c.Quantity);
        if (item.Interest == "" || item.Interest.StartsWith("Magic: The Gathering")) item.Interest = Interest(c, kr);
        item.PriceHint = Pricing.ShopPrice(kr);
        item.Touch();
        return true;
    }

    public static Item NewItem(MagicCardInfo c, int boxId)
    {
        var kr = Kr(c.EurPrice);
        var item = new Item
        {
            Kind = Kinds.Magic, Title = c.Name, Series = c.SetName, Number = c.CollectorNumber, Year = c.ReleaseYear,
            Quantity = Math.Max(1, c.Quantity), Condition = "God", BoxId = boxId, MagicRef = c.Id.ToString(),
            PriceHint = Pricing.ShopPrice(kr), Interest = Interest(c, kr), Confidence = 1,
        };
        item.Touch();
        return item;
    }
}
