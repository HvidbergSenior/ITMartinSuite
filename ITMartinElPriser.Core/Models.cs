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

    // Set from the postcode (Strømligning grid id, e.g. "konstant_c"). When set, the exact
    // published tariff per quarter-hour replaces the GridCompanyId preset bands.
    public string PostalCode { get; set; } = "";
    public string GridSupplierId { get; set; } = "";
    public string GridName { get; set; } = "";

    // Filled at runtime by GridTariffs.AttachAsync - never stored. kr/kWh ex VAT, or null.
    [System.Text.Json.Serialization.JsonIgnore]
    public Func<DateTime, double?>? NettarifLookup { get; set; }
    public double CustomNettarifOre { get; set; } = 25;
    public string SupplierId { get; set; } = "nrgi-time";
    public double CustomTillaegOre { get; set; }
    // Hours nobody would start a machine anyway - windows are not suggested
    // inside them, so "cheapest" is cheapest when someone is actually up.
    public int QuietFromHour { get; set; } = 23;
    public int QuietToHour { get; set; } = 6;

    // "I nat" card: how much the EV needs and how fast the charger is. Kept
    // here so the free app remembers it in the cookie like the tariffs.
    public double EvKwh { get; set; } = 30;
    public double EvKw { get; set; } = 11;

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
    // Model number from the plate/energy label, e.g. "Asko W6884" - shown
    // with the name so the kWh can be checked against the right manual.
    public string Model { get; set; } = "";
    public double KwhPerRun { get; set; }
    public double DurationHours { get; set; }
    // How often it typically runs - turns "kr pr. tur" into kr pr. måned.
    public double RunsPerWeek { get; set; } = 3;
    public int SortOrder { get; set; }

    // Optional programs (30/40/60/90 °C, Eco/Normal). When there are any, the
    // flat fields above are kept in sync as the weekly mix (see
    // SyncFromPrograms), so everything that only knows "one run" still adds
    // up right, and a single program is picked with WithProgram.
    public List<ApplianceProgram> Programs { get; set; } = [];

    // The first program is the one used most, so the flat KwhPerRun (what
    // the month price and the notifications use) follows it. The run time
    // and runs per week belong to the machine: a washer takes 2 hours
    // whatever the temperature, and nobody should have to count washes.
    public void SyncFromPrograms()
    {
        Programs.RemoveAll(p => string.IsNullOrWhiteSpace(p.Name) || p.KwhPerRun <= 0);
        if (Programs.Count == 0) return;
        KwhPerRun = Programs[0].KwhPerRun;
        foreach (var p in Programs) { p.DurationHours = DurationHours; p.RunsPerWeek = 0; }
    }

    // The same machine running one program - same Id, so run logging and
    // history still point at it. Out-of-range or no programs = itself.
    public Appliance WithProgram(int? index) =>
        index is int i && i >= 0 && i < Programs.Count
            ? new Appliance
            {
                Id = Id, Name = Name, Icon = Icon, Model = Model, SortOrder = SortOrder, Programs = Programs,
                KwhPerRun = Programs[i].KwhPerRun, DurationHours = DurationHours, RunsPerWeek = RunsPerWeek,
            }
            : this;

    public Appliance Clone() => new()
    {
        Id = Id, Name = Name, Icon = Icon, Model = Model, KwhPerRun = KwhPerRun, DurationHours = DurationHours, RunsPerWeek = RunsPerWeek, SortOrder = SortOrder,
        Programs = Programs.Select(p => new ApplianceProgram { Name = p.Name, KwhPerRun = p.KwhPerRun, DurationHours = p.DurationHours, RunsPerWeek = p.RunsPerWeek }).ToList(),
    };

    public static List<Appliance> Defaults() =>
    [
        new() { Name = "Vaskemaskine", Icon = "🧺", KwhPerRun = 1.0, DurationHours = 2, RunsPerWeek = 4, SortOrder = 0 },
        new() { Name = "Opvaskemaskine", Icon = "🍽️", KwhPerRun = 1.2, DurationHours = 2, RunsPerWeek = 5, SortOrder = 1 },
        new() { Name = "Tørretumbler", Icon = "🌀", KwhPerRun = 2.5, DurationHours = 1.5, RunsPerWeek = 2, SortOrder = 2 },
    ];
}

// One program on a machine, e.g. "40 °C" at 0.7 kWh over 2 hours.
public sealed class ApplianceProgram
{
    public string Name { get; set; } = "";
    public double KwhPerRun { get; set; }
    public double DurationHours { get; set; }
    public double RunsPerWeek { get; set; }
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
    // Model number from the energy label, e.g. "Samsung RB38" - used by the AI lookup.
    public string Model { get; set; } = "";
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
