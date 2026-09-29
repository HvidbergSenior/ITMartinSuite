using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace ITMartinElPriser.Core;

// One electricity product from one supplier. Money is ex VAT, as the source gives it.
public sealed record SupplierProduct(
    string Id, string Company, string Name, bool Fixed,
    double SurchargeKrPerKwh, double FixedKrPerKwh, double SubscriptionMonthly, double FeesMonthly,
    double CreationFee, string Binding, string Link, DateTime? Updated,
    bool Intro, double? MinKwh, double? MaxKwh,
    // What you get besides the price (elpris.dk): notice, payment, renewable share, start.
    string Termination = "", string PaymentMethods = "", double? RenewableShare = null, string DeliveryStart = "")
{
    // Comparable for this household: no short intro offers, no odd "negative markup" deals
    // (those pair with a big subscription), and inside the product's consumption limits.
    public bool FitsFor(double kwh) =>
        !Intro && SurchargeKrPerKwh >= 0 && (Fixed || SurchargeKrPerKwh <= BillComparison.MaxPlausibleMarkupKr) &&
        (MinKwh is null || kwh >= MinKwh) && (MaxKwh is null || kwh <= MaxKwh);

    public const double Vat = 1.25;

    // What differs between suppliers for a year's use: the markup (or the fixed price,
    // which includes the power itself), subscription and payment fees - incl. VAT.
    // Spot price, elafgift and the grid tariff are the same whoever you buy from.
    public double YearlyKr(double kwh) =>
        ((Fixed ? FixedKrPerKwh : SurchargeKrPerKwh) * kwh + (SubscriptionMonthly + FeesMonthly) * 12) * Vat;
}

// Supplier products from elpris.dk - Forsyningstilsynet's official price portal, which every
// supplier must report to. One static file per grid area (products_<gridArea>.json), fetched
// once a day per area for everyone, never per visitor. Replaced Strømligning 2026-09-28: their
// data is CC BY-NC (non-commercial), and MinElpris is meant to be a paid product.
public sealed class SupplierCatalog(HttpClient http, ILogger<SupplierCatalog> logger)
{
    public const string Source = "elpris.dk (Forsyningstilsynet)";
    public const string DefaultGridArea = "151"; // Konstant Net A/S (Aarhus)

    private readonly Dictionary<string, (DateTime Fetched, List<SupplierProduct> Products)> _byArea = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    public DateTime? FetchedUtc { get; private set; }

    public async Task<List<SupplierProduct>> GetAsync(string? gridArea = null)
    {
        var area = string.IsNullOrWhiteSpace(gridArea) ? DefaultGridArea : gridArea.Trim();
        if (Fresh(area, out var cached)) return cached;
        await _gate.WaitAsync();
        try
        {
            if (Fresh(area, out cached)) return cached;
            var file = await http.GetFromJsonAsync<AreaFile>($"https://elpris.dk/data/products_{area}.json");
            var list = (file?.Products ?? [])
                .Where(p => p.Private)                 // households only - MinElpris is not for businesses
                .Select(Map)
                .OfType<SupplierProduct>()
                .ToList();
            if (list.Count > 0)
            {
                _byArea[area] = (DateTime.UtcNow, list);
                FetchedUtc = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            // Keep yesterday's list rather than showing nothing; retry in an hour.
            logger.LogWarning(ex, "Supplier catalogue fetch failed for grid area {Area}", area);
            if (_byArea.TryGetValue(area, out var old)) _byArea[area] = (DateTime.UtcNow.AddHours(-23), old.Products);
        }
        finally { _gate.Release(); }
        return _byArea.TryGetValue(area, out var hit) ? hit.Products : [];
    }

    private bool Fresh(string area, out List<SupplierProduct> products)
    {
        products = [];
        if (!_byArea.TryGetValue(area, out var hit) || DateTime.UtcNow - hit.Fetched >= TimeSpan.FromHours(24) || hit.Products.Count == 0)
            return false;
        products = hit.Products;
        return true;
    }

    private static SupplierProduct? Map(Product p)
    {
        var now = DateTime.Today;
        var price = p.ProductPrices
            .OrderByDescending(x => x.ValidFrom <= now && (x.ValidTo is null || x.ValidTo >= now))
            .ThenByDescending(x => x.ValidFrom)
            .FirstOrDefault();
        if (price is null) return null;

        // The matrix can split the day (time-of-day prices) and the year (volume steps).
        // Compare on the hour-weighted price of the first volume step.
        var rows = price.Matrix.Where(m => m.VolumeFrom <= 0).ToList();
        if (rows.Count == 0) rows = price.Matrix;
        var hours = rows.Sum(m => Math.Max(0, m.HoursTo - m.HoursFrom));
        var ore = hours > 0 ? rows.Sum(m => m.Amount * Math.Max(0, m.HoursTo - m.HoursFrom)) / hours : rows.Select(m => m.Amount).DefaultIfEmpty(0).Average();
        var minVol = price.Matrix.Select(m => m.VolumeFrom).DefaultIfEmpty(0).Min();
        var maxVol = price.Matrix.Select(m => m.VolumeTo).DefaultIfEmpty(0).Max();

        var isFixed = !price.AdditionalNordPoolSpot;
        var cheapestPaymentFee = p.PaymentMethods.Select(m => m.PaymentMethodFee ?? 0).DefaultIfEmpty(0).Min();
        var company = p.Supplier.Name.Split(" - ")[0].Trim();   // "NRGi Elhandel A/S - 7011 4500"

        return new SupplierProduct(
            $"ep-{p.ProductId}-{p.ProductVariantId}", company, p.Name.Trim(), isFixed,
            isFixed ? 0 : ore / 100, isFixed ? ore / 100 : 0, price.Subscription, cheapestPaymentFee,
            0, (p.BindingPeriod ?? "").Trim(), p.OrderUrl ?? p.ReadmoreUrl ?? p.Supplier.HomepageUrl ?? "", price.LastUpdate,
            p.IntroOffer, minVol > 0 ? minVol : null, maxVol is > 0 and < 999999 ? maxVol : null,
            Termination: (p.Termination ?? "").Trim(),
            PaymentMethods: string.Join(", ", p.PaymentMethods.Select(m => m.Name).Where(n => !string.IsNullOrWhiteSpace(n))),
            RenewableShare: p.Climate?.SustainableEnergyLevel,
            DeliveryStart: (p.DeliveryStart ?? "").Trim());
    }

    private sealed class AreaFile
    {
        [JsonPropertyName("products")] public List<Product> Products { get; set; } = [];
    }

    private sealed class Product
    {
        [JsonPropertyName("productId")] public int ProductId { get; set; }
        [JsonPropertyName("productVariantId")] public int ProductVariantId { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("supplier")] public Supplier Supplier { get; set; } = new();
        [JsonPropertyName("private1")] public bool Private { get; set; }
        [JsonPropertyName("introOffer")] public bool IntroOffer { get; set; }
        [JsonPropertyName("bindingPeriod")] public string? BindingPeriod { get; set; }
        [JsonPropertyName("termination")] public string? Termination { get; set; }
        [JsonPropertyName("deliveryStart")] public string? DeliveryStart { get; set; }
        [JsonPropertyName("orderUrl")] public string? OrderUrl { get; set; }
        [JsonPropertyName("readmoreUrl")] public string? ReadmoreUrl { get; set; }
        [JsonPropertyName("paymentMethods")] public List<PaymentMethod> PaymentMethods { get; set; } = [];
        [JsonPropertyName("climate")] public Climate? Climate { get; set; }
        [JsonPropertyName("productPrices")] public List<Price> ProductPrices { get; set; } = [];
    }

    private sealed class Supplier
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("homepageUrl")] public string? HomepageUrl { get; set; }
    }

    private sealed class PaymentMethod
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("paymentMethodFee")] public double? PaymentMethodFee { get; set; }
    }

    private sealed class Climate
    {
        [JsonPropertyName("sustainableEnergyLevel")] public double? SustainableEnergyLevel { get; set; }
    }

    private sealed class Price
    {
        [JsonPropertyName("subscription")] public double Subscription { get; set; }
        [JsonPropertyName("additionalNordPoolSpot")] public bool AdditionalNordPoolSpot { get; set; }
        [JsonPropertyName("validFrom")] public DateTime? ValidFrom { get; set; }
        [JsonPropertyName("validTo")] public DateTime? ValidTo { get; set; }
        [JsonPropertyName("lastUpdate")] public DateTime? LastUpdate { get; set; }
        [JsonPropertyName("matrix")] public List<MatrixRow> Matrix { get; set; } = [];
    }

    private sealed class MatrixRow
    {
        [JsonPropertyName("hoursFrom")] public double HoursFrom { get; set; }
        [JsonPropertyName("hoursTo")] public double HoursTo { get; set; }
        [JsonPropertyName("volumeFrom")] public double VolumeFrom { get; set; }
        [JsonPropertyName("volumeTo")] public double VolumeTo { get; set; }
        [JsonPropertyName("amount")] public double Amount { get; set; }
    }
}
