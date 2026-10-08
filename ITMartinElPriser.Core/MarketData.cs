namespace ITMartinElPriser.Core;

// The data the outside services deliver (Energinet, Strømligning, elpris.dk), as plain domain types.
// The services themselves live in ITMartinElPriser.Infrastructure.

// How much CO2 one kWh from the Danish grid carries, in grams, per 5 minutes.
public sealed record Co2Point(DateTime TimeDk, double Grams);

public sealed record Co2Snapshot(Co2Point? Now, List<Co2Point> Forecast, double? DayAvg);

// Denmark's production right now (whole country, MW): wind, sun, power stations, and the
// net import (negative = we export).
public sealed record PowerMix(DateTime TimeDk, double Wind, double Solar, double Plants, double NetImport)
{
    public double Consumption => Math.Max(1, Wind + Solar + Plants + NetImport);
    public double Share(double mw) => Math.Clamp(mw / Consumption, 0, 1);
}

// One 15-minute slot of the day-ahead market. The Danish market went from
// hourly to quarter-hourly in 2025; Energinet's old "Elspotprices" dataset
// now returns nothing, "DayAheadPrices" is the live one.
public sealed class PricePoint
{
    public DateTime TimeUtc { get; set; }
    public DateTime TimeDk { get; set; }
    public double PriceKrPerKwh { get; set; }
}

public sealed record GridCompany(string Id, string Name, string Company, string PriceArea);

// Where the household is: a readable label, its postcode and the point used to find the
// ONE grid company that serves it (postcodes can be shared by two).
public sealed record Place(string Label, string PostalCode, double Lat, double Lon);

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
