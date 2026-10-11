namespace ITMartinLager.Domain;

// One listing as found - the proof behind a price (user 2026-10-07: "it needs proof"). Original = the price as the
// site shows it when it is not in kr ("9,37 EUR"), so the proof can be checked against the page.
public sealed record Offer(string Name, decimal? Price, string Url, string Original = "");

// Low-High = the realistic asking prices; Wild = the highest "urimelig internetpris" (over 3x the middle), if any.
// Proof = up to 3 listings nearest the middle price. Ambiguous = the title is too broad to know they are this book
// ("Zombie", "Flint" without an author) - then there are no prices, so nothing can be priced from it.
public sealed record Listings(int Count, decimal? Low, decimal? Median, decimal? High, string Url, List<Offer> Proof,
    decimal? Wild = null, bool Ambiguous = false, decimal? NewPrice = null)
{
    public bool HasPrices => Low is not null;

    // Every listing that matched the title (priced or not) - the price hunter keeps a selection of them per item.
    public List<Offer> All { get; init; } = [];

    public string Text => Count == 0 ? "ingen til salg"
        : Ambiguous ? $"{Count} fund, men titlen er for bred til at vide om det er denne bog – ikke bevis"
        : Count == 0 && NewPrice is { } np0 ? $"ingen brugte, ny i butikken {np0:0} kr"
        : Low is null ? $"{Count} til salg"
        : $"{Count} til salg, {Low:0}-{High:0} kr (midt {Median:0} kr)" + (Wild is { } w ? $", urimelige internetpriser op til {w:0} kr" : "")
            + (NewPrice is { } np ? $", ny i butikken {np:0} kr" : "");
}
