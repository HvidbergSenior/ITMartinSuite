using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace ITMartinElPriser.Core;

// One electricity product from one supplier. Money is ex VAT, as the source gives it.
public sealed record SupplierProduct(
    string Id, string Company, string Name, bool Fixed,
    double SurchargeKrPerKwh, double FixedKrPerKwh, double SubscriptionMonthly, double FeesMonthly,
    double CreationFee, string Binding, string Link, DateTime? Updated,
    bool Intro, double? MinKwh, double? MaxKwh)
{
    // Comparable for this household: no short intro offers, no odd "negative markup" deals
    // (those pair with a big subscription), and inside the product's consumption limits.
    public bool FitsFor(double kwh) =>
        !Intro && SurchargeKrPerKwh >= 0 && (MinKwh is null || kwh >= MinKwh) && (MaxKwh is null || kwh <= MaxKwh);

    public const double Vat = 1.25;

    // What differs between suppliers for a year's use: the markup (or the fixed price,
    // which includes the power itself), subscription and payment fees - incl. VAT.
    // Spot price, elafgift and the grid tariff are the same whoever you buy from.
    public double YearlyKr(double kwh) =>
        ((Fixed ? FixedKrPerKwh : SurchargeKrPerKwh) * kwh + (SubscriptionMonthly + FeesMonthly) * 12) * Vat;
}

// Supplier products from Strømligning's open API (CC BY-NC 4.0 - non-commercial use with
// attribution, so only the free ElPriser shows it). Fetched once a day for everyone,
// never per visitor, to stay far inside their rate limit.
public sealed class SupplierCatalog(HttpClient http, ILogger<SupplierCatalog> logger)
{
    private const string Url = "https://stromligning.dk/api/companies";
    private List<SupplierProduct> _products = [];
    private DateTime _fetchedUtc = DateTime.MinValue;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public DateTime? FetchedUtc => _fetchedUtc == DateTime.MinValue ? null : _fetchedUtc;

    public async Task<List<SupplierProduct>> GetAsync()
    {
        if (DateTime.UtcNow - _fetchedUtc < TimeSpan.FromHours(24) && _products.Count > 0) return _products;
        await _gate.WaitAsync();
        try
        {
            if (DateTime.UtcNow - _fetchedUtc < TimeSpan.FromHours(24) && _products.Count > 0) return _products;
            var companies = await http.GetFromJsonAsync<List<Company>>(Url) ?? [];
            var list = companies
                .SelectMany(c => c.Products.Select(p => (c, p)))
                .Where(x => x.p.ExcludeInComparison != true && x.p.Disabled != true && x.p.Hidden != true && x.p.Custom != true)
                .Select(x => new SupplierProduct(
                    x.p.Id, x.c.Name.Trim(), x.p.Name.Trim(), x.p.ProductType == "fixed",
                    x.p.ConsumptionSurcharge ?? 0, x.p.FixedPrice ?? 0, x.p.SubscriptionMonthly ?? 0, x.p.FeesMonthly ?? 0,
                    x.p.CreationFee ?? 0, (x.p.BindingPeriod ?? "").Trim(), x.p.Link ?? "", x.p.Updated,
                    x.p.IntroductoryOffer == true, x.p.MinConsumption, x.p.MaxConsumption))
                .ToList();
            if (list.Count > 0) { _products = list; _fetchedUtc = DateTime.UtcNow; }
        }
        catch (Exception ex)
        {
            // Keep yesterday's list rather than showing nothing; retry in an hour.
            logger.LogWarning(ex, "Supplier catalogue fetch failed");
            if (_products.Count > 0) _fetchedUtc = DateTime.UtcNow.AddHours(-23);
        }
        finally { _gate.Release(); }
        return _products;
    }

    private sealed class Company
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("products")] public List<Product> Products { get; set; } = [];
    }

    private sealed class Product
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("productType")] public string ProductType { get; set; } = "";
        [JsonPropertyName("custom")] public bool? Custom { get; set; }
        [JsonPropertyName("consumptionSurcharge")] public double? ConsumptionSurcharge { get; set; }
        [JsonPropertyName("fixedPrice")] public double? FixedPrice { get; set; }
        [JsonPropertyName("subscriptionMonthly")] public double? SubscriptionMonthly { get; set; }
        [JsonPropertyName("feesMonthly")] public double? FeesMonthly { get; set; }
        [JsonPropertyName("creationFee")] public double? CreationFee { get; set; }
        [JsonPropertyName("bindingPeriod")] public string? BindingPeriod { get; set; }
        [JsonPropertyName("link")] public string? Link { get; set; }
        [JsonPropertyName("updated")] public DateTime? Updated { get; set; }
        [JsonPropertyName("excludeInComparison")] public bool? ExcludeInComparison { get; set; }
        [JsonPropertyName("disabled")] public bool? Disabled { get; set; }
        [JsonPropertyName("hidden")] public bool? Hidden { get; set; }
        [JsonPropertyName("introductoryOffer")] public bool? IntroductoryOffer { get; set; }
        [JsonPropertyName("minConsumption")] public double? MinConsumption { get; set; }
        [JsonPropertyName("maxConsumption")] public double? MaxConsumption { get; set; }
    }
}
