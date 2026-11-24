using ITMartinElPriser.Domain;

namespace ITMartinMitEl.Server.Services;

/// <summary>Collects what a customer's household data shows, for the advisor's checklist and the handover sheet.
/// The judging lives in CustomerSetup (Domain); this only reads. Callers check advisor access first.</summary>
public sealed class CustomerSetupReader(HouseholdRegistry registry, AdvisorService advisors)
{
    public async Task<(SetupFacts Facts, HouseholdData Data)?> ReadAsync(Guid advisorUserId, Guid householdId)
    {
        if (!await advisors.HasAccessAsync(advisorUserId, householdId)) return null;
        var customer = (await advisors.CustomersAsync(advisorUserId)).FirstOrDefault(c => c.HouseholdId == householdId);
        var data = registry.Household(householdId).Get();
        return (Facts(householdId, customer), data);
    }

    /// <summary>"8/11" on the Kunder list.</summary>
    public string Progress(Guid householdId)
    {
        var (done, total) = CustomerSetup.Progress(
            CustomerSetup.Evaluate(Facts(householdId, null), registry.Household(householdId).Get().SetupTicks));
        return $"{done}/{total}";
    }

    private SetupFacts Facts(Guid householdId, AdvisorCustomer? customer)
    {
        var data = registry.Household(householdId).Get();
        var s = data.Settings;
        return new SetupFacts(
            HouseholdName: customer?.HouseholdName ?? "",
            OwnerEmail: customer?.OwnerEmail ?? "",
            MeterConnected: s.EloverblikToken.Length > 0 && s.MeteringPointId.Length > 0,
            MeterAddress: s.MeteringPointAddress,
            ConsumptionUntil: registry.Consumption(householdId).LatestDay,
            GridName: s.GridName,
            PriceArea: s.PriceArea,
            LatestBill: data.Bills.FirstOrDefault(),
            Machines: [.. data.Appliances.Where(a => a.Model.Length > 0).Select(a => $"{a.Name} ({a.Model})")],
            DeviceCount: data.Devices.Count,
            NotifyPhones: data.Subscribers.Count(p => p.Failures < 3),
            AdvisorAccessUntilUtc: customer?.ExpiresUtc);
    }

    public void Tick(Guid householdId, string stepId, bool done) =>
        registry.Household(householdId).Update(d =>
        {
            if (done) d.SetupTicks[stepId] = DateTime.UtcNow;
            else d.SetupTicks.Remove(stepId);
        });

    public void SaveNotes(Guid householdId, string notes) =>
        registry.Household(householdId).Update(d => d.AdvisorNotes = (notes ?? "").Trim());
}
