using ITMartinElPriser.Domain;

namespace ITMartinElPriser.Application;

/// <summary>Day-ahead spot prices per 15 minutes (Energinet's DayAheadPrices in production, cached 30 min).</summary>
public interface IPriceSource
{
    /// <summary>Today and - after about 13:00 - tomorrow.</summary>
    Task<List<PricePoint>> GetPricesAsync(string priceArea = "DK1", CancellationToken ct = default);
    Task<List<PricePoint>> GetDayAsync(DateOnly date, string priceArea = "DK1", CancellationToken ct = default);
    Task<List<PricePoint>> GetRangeAsync(DateOnly fromInclusive, DateOnly toExclusive, string priceArea = "DK1", CancellationToken ct = default);
}

/// <summary>CO2 per kWh and the power mix right now (Energinet).</summary>
public interface ICo2Source
{
    Task<Co2Snapshot> GetAsync(string area);
    Task<PowerMix?> GetMixAsync();
}

/// <summary>Where the household is and which grid company (and tariff) it pays - from coordinates or an address.</summary>
public interface IGridTariffSource
{
    Task<Place?> ResolveAsync(string text);
    Task<Place?> ReverseAsync(double lat, double lon);
    Task<GridCompany?> FindAtAsync(double lat, double lon);
    Task<List<GridCompany>> FindAsync(string postalCode);
    /// <summary>Attaches the exact grid tariff to the settings (from the grid company id).</summary>
    Task AttachAsync(HouseholdSettings settings);
    Task<List<(int Hour, double OreInclVat)>> TodayAsync(string gridId);
    Task<List<(DateTime Hour, double Price)>> TomorrowForecastAsync(string area);
}

/// <summary>Every supplier's products for a grid area (elpris.dk, Forsyningstilsynet's portal).</summary>
public interface ISupplierSource
{
    DateTime? FetchedUtc { get; }
    Task<List<SupplierProduct>> GetAsync(string? gridArea = null);
}

/// <summary>The household's own settings. In the free app they live in a cookie on the visitor's phone - nothing about
/// the visitor is kept on the server.</summary>
public interface IHouseholdPrefs
{
    /// <summary>As saved (no tariff looked up).</summary>
    HouseholdSettings Get();
    /// <summary>With the grid company's exact tariff attached - use this whenever prices are computed.</summary>
    Task<HouseholdSettings> GetAsync();
    Task SaveAsync(HouseholdSettings settings);
}

/// <summary>The phones that asked for messages (one JSON file on the data volume).</summary>
public interface ISubscriberStore
{
    SubscriberData Get();
    /// <summary>Changes and saves in one go.</summary>
    void Update(Action<SubscriberData> change);
}

/// <summary>Web Push (VAPID). The keys are made once and kept - new keys would silently orphan every phone.</summary>
public interface IPushSender
{
    string PublicKey { get; }
    /// <summary>False when the phone is gone (it is then removed from the store).</summary>
    Task<bool> SendAsync(PushSubscriber to, PushMessage message, CancellationToken ct = default);
}

/// <summary>The text of a PDF, page by page in reading order.</summary>
public interface IPdfText
{
    string Read(byte[] pdf);
}
