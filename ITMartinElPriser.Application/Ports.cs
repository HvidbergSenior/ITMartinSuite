using ITMartinElPriser.Core;

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
