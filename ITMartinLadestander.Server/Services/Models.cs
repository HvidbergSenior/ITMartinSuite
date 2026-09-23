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

    // The chosen solution (2026-09-23): the association owns Zaptec chargers, Spirii runs access and billing,
    // this app shows prices, live status, each household's consumption and checks Spirii's billing. Every outlet
    // has a data source: "spirii" = live from the Spirii API (the chargers report to Spirii), "zaptec" = straight
    // from Zaptec, "manual" = typed in on Manuel (the old Looad stand, until we know if it can move to Spirii).
    // Without API keys the simulator stands in for Spirii/Zaptec.
    public List<OutletConfig> OutletSetup { get; set; } = [];
    public List<Household> HouseholdList { get; set; } = [];
    // What the association adds per kWh on top of the meter cost (ex moms) - covers Spirii, chargers, buffer.
    public double TillaegOrePerKwh { get; set; } = 30;

    // Booking and queue are agreements between neighbours, not locks: the chargers do not know about them, so
    // charging works exactly the same when this app is down.
    public List<Booking> Bookings { get; set; } = [];
    public List<QueueEntry> Queue { get; set; } = [];
}

public sealed class Booking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public int Outlet { get; set; }
    public DateTime StartDk { get; set; }
    public DateTime EndDk { get; set; }
    public double Kwh { get; set; }
    public double? EstimatedKr { get; set; }
    public DateTime CreatedDk { get; set; }
}

public sealed class QueueEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public double Kwh { get; set; } = 20;
    public DateTime JoinedDk { get; set; }
}

public sealed class OutletConfig
{
    public int Number { get; set; }
    public string Name { get; set; } = "";        // "Stander B · udtag 1"
    public string Source { get; set; } = "spirii"; // "spirii" | "zaptec" | "manual"
    public string SpiriiEvseId { get; set; } = "";    // EVSE id from Spirii Connect (e.g. DK*SPI*E123*1)
    public string ZaptecChargerId { get; set; } = "";
    public double MaxKw { get; set; } = 11;
}

// A house in the association. Tokens = the RFID tags / Zaptec user names that identify its charging sessions.
public sealed class Household
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";         // "Nr. 222 · Martin"
    public List<string> Tokens { get; set; } = [];
}

public sealed class Car
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Owner { get; set; } = "";       // "Martin 222"
    public string Name { get; set; } = "";        // "ID.4"
    public double BatteryKwh { get; set; } = 60;
    public double MaxKw { get; set; } = 11;
    public double TypicalKwh { get; set; } = 30;  // what a normal top-up needs
    public Guid? HouseholdId { get; set; }        // links hand-typed sessions to a household on Forbrug
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

    // Sessions read from the chargers (Zaptec) carry their source id and who charged.
    public string Source { get; set; } = "manual"; // "manual" | "zaptec" | "simulator"
    public string ExternalId { get; set; } = "";
    public string Token { get; set; } = "";
    public Guid? HouseholdId { get; set; }
    // What Spirii actually charged the resident (incl. moms) - compared with UserPriceKr on Afregning.
    public double? BilledKr { get; set; }
}
