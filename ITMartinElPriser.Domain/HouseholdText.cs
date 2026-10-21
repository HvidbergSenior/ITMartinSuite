namespace ITMartinElPriser.Domain;

public static class HouseholdText
{
    /// <summary>Short label for what the prices are worked out with: "DK1 · N1 · NRGi Time".</summary>
    public static string Describe(HouseholdSettings s)
    {
        if (!s.ShowAllIn) return $"Spotpris · {s.PriceArea}";
        var grid = s.GridName.Length > 0 ? s.GridName : GridCompanyPreset.All.FirstOrDefault(g => g.Id == s.GridCompanyId)?.Name ?? "eget net";
        var sup = SupplierPreset.All.FirstOrDefault(p => p.Id == s.SupplierId)?.Name ?? "eget tillæg";
        return $"{s.PriceArea} · {grid} · {sup}";
    }
}
