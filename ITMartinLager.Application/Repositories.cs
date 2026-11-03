using ITMartinLager.Domain;

namespace ITMartinLager.Application;

// The register's storage. Every write method saves at once. Implemented with EF Core + SQLite in Infrastructure.

public sealed record BoxSummary(int Id, string Code, string Place, int Lines, int Units, string Note = "");

public interface IBoxRepository
{
    /// <summary>A new box with the next code (K0001, K0002 ...).</summary>
    Task<Box> AddNextAsync(string place, CancellationToken ct);
    /// <summary>The box with this code (K0007 = NR 7 written on it), made the first time it is seen.</summary>
    Task<Box> GetOrAddAsync(string code, CancellationToken ct);
    Task<Box?> GetAsync(int id, CancellationToken ct);
    Task<Box?> ByCodeAsync(string code, CancellationToken ct);
    Task<List<Box>> RecentAsync(int count, CancellationToken ct);
    Task<List<BoxSummary>> SummariesAsync(CancellationToken ct);
    Task<int> CountAsync(CancellationToken ct);
    Task SetPlaceAsync(int id, string place, CancellationToken ct);
    Task SetNoteAsync(int id, string note, CancellationToken ct);
    Task SetFundAsync(int id, CancellationToken ct);
    /// <summary>The Fund boxes, newest first.</summary>
    Task<List<Box>> FundBoxesAsync(CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

public interface IItemRepository
{
    /// <summary>With its box.</summary>
    Task<Item?> GetAsync(int id, CancellationToken ct);
    Task<List<Item>> InBoxAsync(int boxId, CancellationToken ct);
    /// <summary>Unsold items with one of these titles (Item.TitleKey: case, spaces and punctuation ignored).</summary>
    Task<List<Item>> WithTitlesAsync(IReadOnlyCollection<string> titles, CancellationToken ct, IReadOnlyCollection<string>? barcodes = null);
    /// <summary>Every word must be in the item's search text; with its box.</summary>
    Task<List<Item>> SearchAsync(string text, string? kind, string? valuation, int max, CancellationToken ct, ItemScope? scope = null);
    /// <summary>How many SearchAsync would find without the max.</summary>
    Task<int> CountAsync(string text, string? kind, string? valuation, CancellationToken ct, ItemScope? scope = null);
    /// <summary>Boxes that got items registered since this moment, with how many - newest first.</summary>
    Task<List<(Box Box, int Count)>> NewPerBoxAsync(DateTime sinceUtc, CancellationToken ct);
    /// <summary>The marketplace: things for sale (Shop.Listed) of these kinds (null = all), new or used (null = both), every word in the search
    /// text, sorted (ShopSorts), one page; and how many match in all.</summary>
    Task<(List<Item> Page, int Total)> ListedAsync(IReadOnlyCollection<string>? kinds, string? usage, string text, string sort, int skip, int take, CancellationToken ct,
        string? band = null, IReadOnlyCollection<string>? categories = null, string? language = null);
    /// <summary>Things for sale per main category (Item.Category), in one language or all (null).</summary>
    Task<Dictionary<string, int>> ListedPerCategoryAsync(CancellationToken ct, string? band = null, string? language = null);
    /// <summary>Things for sale per kind.</summary>
    Task<Dictionary<string, int>> ListedPerKindAsync(CancellationToken ct, string? band = null);
    /// <summary>Things for sale of these kinds (null = all) per series - or artist/author when there is no series.</summary>
    Task<Dictionary<string, int>> ListedPerGroupAsync(IReadOnlyCollection<string>? kinds, CancellationToken ct, string? band = null,
        IReadOnlyCollection<string>? categories = null, string? language = null);
    /// <summary>Things for sale per deal (ShopDeals), only the ones in a deal.</summary>
    Task<Dictionary<string, int>> ListedPerDealAsync(CancellationToken ct);
    /// <summary>Every thing for sale, with its box (for Shop.DealFor).</summary>
    Task<List<Item>> AllListedAsync(CancellationToken ct);
    Task<List<Item>> ByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct);
    Task<int> UnitsAsync(CancellationToken ct);
    Task AddAsync(IEnumerable<Item> items, CancellationToken ct);
    Task UpdateAsync(IEnumerable<Item> items, CancellationToken ct);
    /// <summary>The item and its prices.</summary>
    Task DeleteAsync(int id, CancellationToken ct);
    Task<List<Item>> ByMagicRefsAsync(IReadOnlyCollection<string> refs, CancellationToken ct);
    Task<List<Item>> OnAuctionAsync(CancellationToken ct);
    /// <summary>Items chosen for the front pages, newest choice first.</summary>
    Task<List<Item>> FeaturedAsync(int max, CancellationToken ct);
    /// <summary>Hunted kinds, never priced first, then priced before staleBefore.</summary>
    Task<List<int>> NextToHuntAsync(string[] kinds, DateTime staleBefore, int count, CancellationToken ct);
    /// <summary>Books without a kolofon photo that have a used find at fromKr or more, or a 1st-edition cover; highest find first.</summary>
    Task<List<(Item Item, int TopKr)>> KolofonWantedAsync(int fromKr, int max, CancellationToken ct);
    /// <summary>Books with a kolofon photo that is not read yet, oldest first; with their boxes.</summary>
    Task<List<Item>> KolofonWaitingAsync(int max, CancellationToken ct);
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
    /// <summary>All finds of these items, per item.</summary>
    Task<Dictionary<int, List<PriceFind>>> FindsForAsync(IReadOnlyCollection<int> itemIds, CancellationToken ct);
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
