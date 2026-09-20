using ITMartinElPriser.Core;

namespace ITMartinLadestander.Server.Services;

// One association per container for now. The slug is here so the file format
// survives the multi-tenant version (one file per forening).
public sealed class ForeningData
{
    public string Slug { get; set; } = "skelagerhoejen";
    public string Name { get; set; } = "Parcelforeningen Skelagerhøjen";
    public int Households { get; set; } = 30;
    public int Outlets { get; set; } = 2;
    public double OutletKw { get; set; } = 11;

    // Price model: Core does spot + net + Energinet + elafgift + moms; the
    // operator's cut is on top (what a resident pays vs what the meter costs).
    public HouseholdSettings Settings { get; set; } = new()
    {
        PriceArea = "DK1",
        ShowAllIn = true,
        GridCompanyId = "custom",
        CustomNettarifOre = 26.77,     // KONSTANT C-time 33.46 - rabat 6.69 øre ex moms (okt-dec 2024 bill)
        SupplierId = "vindstoed-danskvind",
        QuietFromHour = 22,
        QuietToHour = 6,
    };
    public double OperatorCutOrePerKwh { get; set; } = 40; // Looad Cut, ex moms
    public string OperatorName { get; set; } = "Looad";

    public List<Car> Cars { get; set; } = [];
    public List<ChargeSession> Sessions { get; set; } = [];
}

public sealed class Car
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Owner { get; set; } = "";       // "Martin 222"
    public string Name { get; set; } = "";        // "ID.4"
    public double BatteryKwh { get; set; } = 60;
    public double MaxKw { get; set; } = 11;
    public double TypicalKwh { get; set; } = 30;  // what a normal top-up needs
}

public sealed class ChargeSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CarId { get; set; }
    public int Outlet { get; set; } = 1;
    public DateTime StartedAt { get; set; }       // DK local
    public DateTime? EndedAt { get; set; }
    public double PlannedKwh { get; set; }
    public double? Kwh { get; set; }              // filled in when ended
    public double? CostKr { get; set; }           // meter cost incl. moms
    public double? UserPriceKr { get; set; }      // what the resident pays (meter + operator cut)
    public bool Active => EndedAt is null;
}
