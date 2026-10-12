using ITMartinLager.Domain;

namespace ITMartinLager.Application;

// The register's storage. Every write method saves at once. Implemented with EF Core + SQLite in Infrastructure.

public sealed record BoxSummary(int Id, string Code, string Place, int Lines, int Units);

public interface IBoxRepository
{
    /// <summary>A new box with the next code (K0001, K0002 ...).</summary>
    Task<Box> AddNextAsync(string place, CancellationToken ct);
    Task<Box?> GetAsync(int id, CancellationToken ct);
    Task<Box?> ByCodeAsync(string code, CancellationToken ct);
    Task<List<Box>> RecentAsync(int count, CancellationToken ct);
    Task<List<BoxSummary>> SummariesAsync(CancellationToken ct);
    Task<int> CountAsync(CancellationToken ct);
    Task SetPlaceAsync(int id, string place, CancellationToken ct);
}

public interface IItemRepository
{
    /// <summary>With its box.</summary>
    Task<Item?> GetAsync(int id, CancellationToken ct);
    Task<List<Item>> InBoxAsync(int boxId, CancellationToken ct);
    /// <summary>Unsold items with one of these titles (Item.TitleKey: case, spaces and punctuation ignored).</summary>
    Task<List<Item>> WithTitlesAsync(IReadOnlyCollection<string> titles, CancellationToken ct, IReadOnlyCollection<string>? barcodes = null);
    /// <summary>Every word must be in the item's search text; with its box.</summary>
    Task<List<Item>> SearchAsync(string text, string? kind, string? valuation, int max, CancellationToken ct);
    Task<int> UnitsAsync(CancellationToken ct);
    Task AddAsync(IEnumerable<Item> items, CancellationToken ct);
    Task UpdateAsync(IEnumerable<Item> items, CancellationToken ct);
    /// <summary>The item and its prices.</summary>
    Task DeleteAsync(int id, CancellationToken ct);
    Task<List<Item>> ByMagicRefsAsync(IReadOnlyCollection<string> refs, CancellationToken ct);
    Task<List<Item>> OnAuctionAsync(CancellationToken ct);
    /// <summary>Hunted kinds, never priced first, then priced before staleBefore.</summary>
    Task<List<int>> NextToHuntAsync(string[] kinds, DateTime staleBefore, int count, CancellationToken ct);
    /// <summary>Books without a kolofon photo that have a used find at fromKr or more, or a 1st-edition cover; highest find first.</summary>
    Task<List<(Item Item, int TopKr)>> KolofonWantedAsync(int fromKr, int max, CancellationToken ct);
    /// <summary>Everything, with boxes, for the CSV export.</summary>
    Task<List<Item>> AllAsync(CancellationToken ct);
}

public interface IPriceRepository
{
    Task<(List<PriceFind> Finds, List<PriceCheck> Checks)> ForItemAsync(int itemId, CancellationToken ct);
    /// <summary>Throws the item's old checks and finds away and keeps these.</summary>
    Task ReplaceAsync(int itemId, IEnumerable<PriceCheck> checks, IEnumerable<PriceFind> finds, CancellationToken ct);
    /// <summary>The dearest find per item.</summary>
    Task<Dictionary<int, PriceFind>> TopFindsAsync(IReadOnlyCollection<int> itemIds, CancellationToken ct);
}

public interface IBookCheckRepository
{
    Task<BookCheck?> GetAsync(int id, CancellationToken ct);
    /// <summary>Adds (Id 0) or updates.</summary>
    Task SaveAsync(IEnumerable<BookCheck> checks, CancellationToken ct);
    Task<List<BookCheck>> SearchAsync(string text, int max, CancellationToken ct);
    Task<int> CountAsync(CancellationToken ct);
    Task SetBoxAsync(IReadOnlyCollection<int> ids, string boxCode, CancellationToken ct);
    Task<List<BookCheck>> AllAsync(CancellationToken ct);
}
