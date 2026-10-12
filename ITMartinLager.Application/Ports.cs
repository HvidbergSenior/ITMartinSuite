using ITMartinLager.Domain;

namespace ITMartinLager.Application;

// What the use cases need from the outside world. Implemented in ITMartinLager.Infrastructure.

/// <summary>One price site (DBA, eBay, AbeBooks ...). Null = the site could not be asked (blocked, down, not set up).</summary>
public interface IPriceSource
{
    string Name { get; }
    /// <summary>A shop's new price, not a used market.</summary>
    bool IsNew { get; }
    /// <summary>Where it is: "Sverige", "Tyskland" ... or "".</summary>
    string Country { get; }
    Task<Listings?> LookupAsync(string title, string author, string isbn, CancellationToken ct);
    /// <summary>The site's own search page for the book, for a person to check.</summary>
    string SearchUrl(string title, string author, string isbn);
    /// <summary>Whether this site has this kind of thing at all (HipComic only comics, Discogs only CDs ...).</summary>
    bool Fits(string kind) => true;
}

/// <summary>Claude: values a pile of books from the price sites' numbers - batched, under a daily cap.</summary>
public interface IBookValuer
{
    bool Enabled { get; }
    int MaxCallsPerDay { get; }
    Task<int> CallsTodayAsync(CancellationToken ct);
    Task<Dictionary<int, BookValue>> ValueBooksAsync(IReadOnlyList<string> lines, CancellationToken ct);
}

/// <summary>Vareinfo (2026-10-12 split): the app that finds out WHAT things are - reads piles, looks up barcodes, reads
/// kolofons and keeps those photos. Server to server with a shared key.</summary>
public interface IVareinfoGateway
{
    /// <summary>Where a person opens Vareinfo (https://vareinfo.bogshoppen.dk).</summary>
    string PublicBase { get; }
    Task<(int PhotoId, List<Found> Found)> ReadPileAsync(Stream photo, string hint, string label, CancellationToken ct);
    Task<Found?> LookupBarcodeAsync(string code, CancellationToken ct);
}

/// <summary>Photo files in the data folder. Paths are relative to it.</summary>
public interface IPhotoStore
{
    /// <summary>Phone photos (3-12 MB) -> a JPEG of at most ~1568 px, upright.</summary>
    Task<byte[]> ShrinkAsync(Stream photo, CancellationToken ct);
    Task SaveAsync(string path, byte[] jpeg, CancellationToken ct);
    Task<byte[]?> ReadAsync(string path, CancellationToken ct);
    /// <summary>The full file name when the path is inside the data folder and exists, else null.</summary>
    string? FullPath(string path);
}

/// <summary>Live Auktion (auction-web), server to server.</summary>
public interface IAuctionGateway
{
    bool Enabled { get; }
    string PublicBase { get; }
    Task<List<AuctionSessionInfo>> SessionsAsync(CancellationToken ct);
    Task<Guid> SendAsync(string code, string name, string description, decimal startPrice, int lot, byte[]? jpeg, CancellationToken ct);
    Task<List<AuctionStatusInfo>> StatusAsync(IEnumerable<Guid> ids, CancellationToken ct);
}

/// <summary>The MTG Scanner (magic-web).</summary>
public interface IMagicGateway
{
    bool Enabled { get; }
    Task<List<MagicOwner>> OwnersAsync(CancellationToken ct);
    Task<List<MagicCardInfo>> CardsAsync(string owner, CancellationToken ct);
}

/// <summary>Settings the use cases read (Lager__* in magic.env).</summary>
public sealed record LagerOptions
{
    public int KolofonFromKr { get; init; } = Kolofon.DefaultFromKr;
    /// <summary>The key Vareinfo sends (Lager__InfoKey) - its calls to /api/info/* need it.</summary>
    public string InfoKey { get; init; } = "";
}

/// <summary>A message for the person, in Danish - shown as it is.</summary>
public sealed class LagerException(string message) : Exception(message);
