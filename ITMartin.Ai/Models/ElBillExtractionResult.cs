namespace ITMartin.Ai.Models;

public sealed class ElBillExtractionResult
{
    public string? GridCompanyName { get; set; }

    public string? SupplierName { get; set; }

    public decimal? NettarifOrePerKwh { get; set; }

    public decimal? ElafgiftOrePerKwh { get; set; }

    public decimal? SupplierMarkupOrePerKwh { get; set; }

    public decimal? GridMonthlySubscriptionKr { get; set; }

    public decimal? SupplierMonthlySubscriptionKr { get; set; }

    public decimal? TotalAmountKr { get; set; }

    public string? BillingPeriod { get; set; }
}
