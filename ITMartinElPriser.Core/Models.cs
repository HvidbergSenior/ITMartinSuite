namespace ITMartinElPriser.Core;

// Everything needed to turn a raw spot price into what a household pays, plus
// the two kinds of load (a run, a device). Shared by the free ElPriser app and
// Mit El so both price things exactly the same way.
public sealed class HouseholdSettings
{
    public string PriceArea { get; set; } = "DK1";
    // Show all-in kr/kWh (spot + nettarif + afgift + tillæg + moms) or spot only.
    public bool ShowAllIn { get; set; } = true;
    public string GridCompanyId { get; set; } = "n1";
    public double CustomNettarifOre { get; set; } = 25;
    public string SupplierId { get; set; } = "nrgi-time";
    public double CustomTillaegOre { get; set; }
    // Hours nobody would start a machine anyway - windows are not suggested
    // inside them, so "cheapest" is cheapest when someone is actually up.
    public int QuietFromHour { get; set; } = 23;
    public int QuietToHour { get; set; } = 6;

    // eloverblik.dk personal refresh token + which meter to read. Lets the
    // app show what the household actually used, priced hour by hour.
    public string EloverblikToken { get; set; } = "";
    public string MeteringPointId { get; set; } = "";
    public string MeteringPointAddress { get; set; } = "";
}

// A machine and what one run of it draws. Energy is assumed to be spread
// evenly across the run - close enough for a wash or a dishwasher cycle,
// and it lets the run cost be summed slot by slot against the real price
// curve instead of pretending the start-time price holds for two hours.
public sealed class Appliance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "🔌";
    public double KwhPerRun { get; set; }
    public double DurationHours { get; set; }
    // How often it typically runs - turns "kr pr. tur" into kr pr. måned.
    public double RunsPerWeek { get; set; } = 3;
    public int SortOrder { get; set; }

    public static List<Appliance> Defaults() =>
    [
        new() { Name = "Vaskemaskine", Icon = "🧺", KwhPerRun = 1.0, DurationHours = 2, RunsPerWeek = 4, SortOrder = 0 },
        new() { Name = "Opvaskemaskine", Icon = "🍽️", KwhPerRun = 1.2, DurationHours = 2, RunsPerWeek = 5, SortOrder = 1 },
        new() { Name = "Tørretumbler", Icon = "🌀", KwhPerRun = 2.5, DurationHours = 1.5, RunsPerWeek = 2, SortOrder = 2 },
    ];
}

// Everything that is not "a run": the fridge, the router, the TV in the
// evening, the heat pump. Priced by average draw x hours per day. Things
// that are on all day are priced at the day's average; things with a usual
// time of day are priced at that window, and a shiftable one (water heater,
// heat pump boost) also gets the cheapest window it could move to.
public sealed class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "🔌";
    public string CatalogId { get; set; } = "";
    // Average draw while it is on, in watts (a 150 kWh/yr fridge is ~17 W).
    public double Watts { get; set; }
    public double HoursPerDay { get; set; } = 24;
    public int DaysPerWeek { get; set; } = 7;
    // null = spread across the day (priced at the day's average price).
    public int? UsualFromHour { get; set; }
    public bool Shiftable { get; set; }
    public int SortOrder { get; set; }

    public double KwhPerDay => Watts / 1000.0 * Math.Clamp(HoursPerDay, 0, 24) * Math.Clamp(DaysPerWeek, 0, 7) / 7.0;
    public double KwhPerYear => KwhPerDay * 365;
}

// One real run she registered: what it cost, priced slot by slot over its
// duration at the time it ran, plus what the same run would have cost at
// the day's best and worst start. Values are frozen at logging time so
// the history stays true even if the appliance is edited later.
public sealed class RunEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplianceId { get; set; }
    public string ApplianceName { get; set; } = "";
    public string Icon { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public double DurationHours { get; set; }
    public double KwhPerRun { get; set; }
    public double CostKr { get; set; }
    public double AvgKrPerKwh { get; set; }
    public double? CheapestThatDayKr { get; set; }
    public double? DearestThatDayKr { get; set; }
    public bool AllIn { get; set; }
    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;
}
