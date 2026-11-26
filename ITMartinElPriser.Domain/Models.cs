namespace ITMartinElPriser.Domain;

// Everything needed to turn a raw spot price into what a household pays, plus
// the two kinds of load (a run, a device). Shared by the free ElPriser app and
// Mit El so both price things exactly the same way.
public sealed class HouseholdSettings
{
    public string PriceArea { get; set; } = "DK1";
    // Show all-in kr/kWh (spot + nettarif + afgift + tillæg + moms) or spot only.
    public bool ShowAllIn { get; set; } = true;
    public string GridCompanyId { get; set; } = "n1";

    // Set from the address (Energinet DataHub owner, e.g. "Konstant Net A/S - 151", see GridIds). When set, the
    // exact published tariff per hour replaces the GridCompanyId preset bands.
    public string PostalCode { get; set; } = "";
    public string GridSupplierId { get; set; } = "";
    public string GridName { get; set; } = "";

    // Filled at runtime by GridTariffs.AttachAsync - never stored. kr/kWh ex VAT, or null.
    [System.Text.Json.Serialization.JsonIgnore]
    public Func<DateTime, double?>? NettarifLookup { get; set; }
    public double CustomNettarifOre { get; set; } = 25;
    // Default = "Typisk elselskab" (median markup, see SupplierPreset.Typical). Was "nrgi-time" (Martin's own) until
    // 2026-11-24 - every new visitor was told they had NRGi Time.
    public string SupplierId { get; set; } = "typisk";
    public double CustomTillaegOre { get; set; }
    // Hours nobody would start a machine anyway - windows are not suggested
    // inside them, so "cheapest" is cheapest when someone is actually up.
    public int QuietFromHour { get; set; } = 23;
    public int QuietToHour { get; set; } = 6;

    // The free front page's machines (user 2026-10-20: "see both washing machine and dishwasher" and "also at night").
    // Hours and kWh are the household's own, kept in the cookie; Machine = the one shown.
    public HomeMachine Dish { get; set; } = HomeMachine.Dishwasher();
    public HomeMachine Wash { get; set; } = HomeMachine.WashingMachine();
    public string Machine { get; set; } = HomeMachine.DishKind;

    // "I nat" card: how much the EV needs and how fast the charger is. Kept
    // here so the free app remembers it in the cookie like the tariffs.
    public double EvKwh { get; set; } = 30;
    public double EvKw { get; set; } = 11;

    // eloverblik.dk personal refresh token + which meter to read. Lets the
    // app show what the household actually used, priced hour by hour.
    public string EloverblikToken { get; set; } = "";
    public string MeteringPointId { get; set; } = "";
    public string MeteringPointAddress { get; set; } = "";

    // What Datahub itself says about the meter: grid company, supplier and the exact
    // charges on it. When present it beats every preset and postcode guess.
    public MeterCharges? Meter { get; set; }
}

// The charges Datahub lists on one metering point (eloverblik getcharges + getdetails).
// All kr ex VAT. Refreshed by the meter sync about once a day.
public sealed class MeterCharges
{
    public string GridOperatorName { get; set; } = "";
    public string SupplierName { get; set; } = "";
    public string MeterReadingOccurrence { get; set; } = "";   // "PT15M" or "PT1H"
    // The grid company's own tariff per hour 0-23, rebates already subtracted.
    public double[] NetTariffByHour { get; set; } = [];
    public double EnerginetKrPerKwh { get; set; }      // transmission + system tariff
    public double ElafgiftKrPerKwh { get; set; }
    public double SubscriptionsKrPerMonth { get; set; }  // grid + Energinet subscriptions
    public DateTime FetchedUtc { get; set; }
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

    // Set from a photo of the rating plate / energy label (MinElpris: camera only, not editable).
    public PhotoFacts? Photo { get; set; }

    // What the meter measured (RunMeasure): a test run, or the average of 3+ normal runs. Null = the label's figure.
    public double? MeasuredKwhPerRun { get; set; }
    public int MeasuredRuns { get; set; }
    public bool MeasuredByTest { get; set; }

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
        Id = Id, Name = Name, Icon = Icon, Model = Model, KwhPerRun = KwhPerRun, DurationHours = DurationHours, RunsPerWeek = RunsPerWeek, SortOrder = SortOrder, Photo = Photo,
        MeasuredKwhPerRun = MeasuredKwhPerRun, MeasuredRuns = MeasuredRuns, MeasuredByTest = MeasuredByTest,
        Programs = Programs.Select(p => new ApplianceProgram { Name = p.Name, KwhPerRun = p.KwhPerRun, DurationHours = p.DurationHours, RunsPerWeek = p.RunsPerWeek }).ToList(),
    };

    public static List<Appliance> Defaults() =>
    [
        new() { Name = "Vaskemaskine", Icon = "🧺", KwhPerRun = 1.0, DurationHours = 2, RunsPerWeek = 4, SortOrder = 0 },
        new() { Name = "Opvaskemaskine", Icon = "🍽️", KwhPerRun = 1.2, DurationHours = 2, RunsPerWeek = 5, SortOrder = 1 },
        new() { Name = "Tørretumbler", Icon = "🌀", KwhPerRun = 2.5, DurationHours = 1.5, RunsPerWeek = 2, SortOrder = 2 },
    ];
}

// What a photo of the plate or energy label told us, plus the energy-saving model it is compared with.
public sealed class PhotoFacts
{
    public string Brand { get; set; } = "";
    public string EnergyClass { get; set; } = "";
    // What the label or the AI says it uses in a year at normal use.
    public double KwhPerYear { get; set; }
    public string Note { get; set; } = "";
    // A new energy-saving model of the same kind, for "what you could save".
    public string NewModelName { get; set; } = "";
    public double NewModelKwhPerYear { get; set; }
    public string NewModelSource { get; set; } = "";
    public DateTime TakenUtc { get; set; } = DateTime.UtcNow;
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

    // Set from a photo of the rating plate / energy label (MinElpris: camera only, not editable).
    public PhotoFacts? Photo { get; set; }

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

    // A calibration run (user 2026-10-29: "test your devices to calibrate... oven on 200 degrees for an hour"):
    // a fixed recipe with nothing else big running - one clean measurement is enough to set the machine's kWh.
    public bool IsTest { get; set; }
    // What the meter says the run really used (its extra draw above the day's base load), once the meter has the day.
    public double? MeasuredKwh { get; set; }
    public bool MeasuredUncertain { get; set; }
    public string MeasureNote { get; set; } = "";
}

/// <summary>A common electric car and what it uses per 100 km in normal mixed driving, mild weather (real-world figures,
/// rounded; user 2026-11-26: "a teaser for elbiler - a couple of models and what the winter matters").</summary>
public sealed record EvExample(string Name, double KwhPer100Km)
{
    // Just a small and a big car and the difference (user 2026-11-26: "just show elbil small car and big car and the difference").
    public static readonly EvExample Small = new("Lille (fx Tesla Model 3)", 15);
    public static readonly EvExample Big = new("Stor (fx Audi Q8 e-tron)", 24);

    /// <summary>Cold (around 0 °C vs ~20 °C): heating and a cold battery - typically 20-40 % more; the middle is used.
    /// It is the temperature, not the season (user 2026-11-26).</summary>
    public const double WinterFactor = 1.3;

    /// <summary>Motorway at ~130 km/t vs mixed driving: typically 20-30 % more; the middle is used (user 2026-11-26: "speed").</summary>
    public const double MotorwayFactor = 1.25;

    /// <summary>The span (user 2026-11-26): best = small car, 110 km/t (+10 %), mild ~10 °C (+10 %);
    /// worst = big car, 130 km/t (+25 %), frost ~-5 °C (+35 %). Typical factors, not measured on these cars.</summary>
    public const double BestFactor = 1.1 * 1.1;
    public const double WorstFactor = 1.25 * 1.35;

    /// <summary>What 100 km costs for this car, scaled from the price of the standard 18 kWh / 100 km at the same time.</summary>
    public double CostKr(RunOption standard, double standardKwh) => standard.CostKr * KwhPer100Km / standardKwh;
}

// One machine on the free front page: what a run takes (kWh spread evenly over the hours).
public sealed class HomeMachine
{
    public const string DishKind = "opvask", WashKind = "vask", BikeKind = "elcykel", CarKind = "elbil", KettleKind = "elkedel";
    /// <summary>The devices the free front page offers, in chip order (user 2026-11-26: "3-4 devices like Opvaskemaskine,
    /// Elcykel, Elbil, Kedel"). The washing machine is still known but not offered.</summary>
    // Elkedel and elcykel taken off again the same day (user: "remove elkedel and elcykel") - still known, not offered.
    public static readonly string[] FrontKinds = [DishKind, CarKind];
    public const double MinHours = 0.5, MaxHours = 4, MinKwh = 0.1, MaxKwh = 5;

    public string Kind { get; set; } = DishKind;
    public double Hours { get; set; } = 2;
    public double Kwh { get; set; } = 1;
    /// <summary>The real machine the standard numbers come from, shown in the free app (user 2026-11-24). "" = none yet.</summary>
    public string Model { get; set; } = "";

    // Dishwasher: Bosch SMD6TCX00E, energy class A, 54 kWh / 100 Eco cycles (EU energy label, EPREL).
    public static HomeMachine Dishwasher() => new() { Kind = DishKind, Hours = 2, Kwh = 0.54, Model = "Bosch SMD6TCX00E, Eco-program" };
    // Washing machine: Bosch Serie 6 WGG244RASN, 9 kg, energy class A - EU label (Bosch da-DK spec sheet, read 2026-11-24):
    // 46 kWh / 100 Eco 40-60 cycles = 0.46 kWh a wash, Eco 40-60 runs 3:48 at full load (3.75 h on the quarter grid).
    public static HomeMachine WashingMachine() => new() { Kind = WashKind, Hours = 3.75, Kwh = 0.46, Model = "Bosch WGG244RASN, Eco 40-60" };

    /// <summary>The free ElPriser's machine: always the standard one, never the visitor's own numbers (user 2026-11-24:
    /// "Ret tid og strøm should not be changeable" - own machines are MinElpris).</summary>
    public static HomeMachine Fixed(string kind) => kind switch
    {
        WashKind => WashingMachine(),
        // An exact amount each (user 2026-11-26: "pr opladning is diffuse ... a vaskemaskine has an exact amount"):
        // e-bike = one full charge from empty, a ~0.5 kWh battery in ~4 h; EV = 100 km at ~18 kWh (a typical family car,
        // all year), ~2 h on an 11 kW home charger; kettle = 1 litre ~0.12 kWh, one quarter on the price grid.
        BikeKind => new() { Kind = BikeKind, Hours = 4, Kwh = 0.5 },
        CarKind => new() { Kind = CarKind, Hours = 2, Kwh = 18 },
        KettleKind => new() { Kind = KettleKind, Hours = 0.25, Kwh = 0.12 },
        // A typical dishwasher a few years old, normal use - not the one new Bosch on Eco (0.54 kWh). User 2026-11-26: "like average
        // but also that it depends" + "devices that are more than 2-3 years old". Older labels ~0.9-1.1 Eco, normal ~1.2.
        _ => new() { Kind = DishKind, Hours = 2, Kwh = 1.2 },   // user 2026-11-26: "write 1,2 kWh"
    };

    /// <summary>What the average depends on, said in one short line under the number.</summary>
    public string DependsOn => Kind switch
    {
        BikeKind => "Gennemsnit – afhænger af batteriet.",
        CarKind => "Gennemsnit – stor bil og kulde bruger mere.",
        KettleKind => "Pr. liter – mere vand, mere strøm.",
        WashKind => "Gennemsnit – afhænger af temperaturen.",
        // No "Eco" (user 2026-11-26: "dont use eco").
        _ => "Gennemsnit – afhænger af maskine og program.",
    };

    public bool IsWash => Kind == WashKind;
    public string Name => Kind switch { WashKind => "Vaskemaskine", BikeKind => "Elcykel", CarKind => "Elbil", KettleKind => "Elkedel", _ => "Opvaskemaskine" };
    // "én vask" / "én opvask" / "én opladning"
    public string Run => Kind switch { WashKind => "vask", BikeKind => "opladning", CarKind => "100 km", KettleKind => "liter vand", _ => "opvask" };
    /// <summary>The exact amount the kWh is for: "pr. opvask", "til 100 km", "fra tom til fuld".</summary>
    public string Amount => Kind switch
    {
        BikeKind => "fra tom til fuld", CarKind => "til 100 km", KettleKind => "pr. liter vand", WashKind => "pr. vask", _ => "pr. opvask",
    };
    /// <summary>"en opvaskemaskine" - the device in a sentence.</summary>
    public string InSentence => Kind switch { WashKind => "en vaskemaskine", BikeKind => "en elcykel", CarKind => "en elbil", KettleKind => "en elkedel", _ => "en opvaskemaskine" };
    /// <summary>Something you would start later (a timer, a charger) - not a kettle you need now.</summary>
    public bool CanWait => Kind != KettleKind;

    /// <summary>Whatever the cookie says: hours on the quarter-hour grid within 0.5-4, kWh within 0.1-5.</summary>
    public HomeMachine Clean()
    {
        Hours = Math.Clamp(Math.Round(Hours * 4) / 4, MinHours, MaxHours);
        Kwh = Math.Clamp(Math.Round(Kwh, 2), MinKwh, MaxKwh);
        if (Kind is not (DishKind or WashKind or BikeKind or CarKind or KettleKind)) Kind = DishKind;
        return this;
    }

    public Appliance ToAppliance() => new() { Name = Name, KwhPerRun = Kwh, DurationHours = Hours };
}
