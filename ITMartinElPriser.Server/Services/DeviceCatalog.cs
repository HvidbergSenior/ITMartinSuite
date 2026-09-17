namespace ITMartinElPriser.Server.Services;

// Typical Danish household devices with starting-point numbers (SparEnergi /
// Bolius / energy-label ballparks). The user ticks what they own and adjusts
// the numbers - the point is to get a complete list quickly, not to be exact
// out of the box. A "run" entry becomes an Appliance (priced per run, can be
// started at the right time); everything else becomes a Device (priced per day).
public sealed record CatalogEntry(
    string Id, string Group, string Icon, string Name,
    bool IsRun,
    // Run entries: kWh per run + hours per run + runs per week.
    double KwhPerRun = 0, double DurationHours = 0, double RunsPerWeek = 0,
    // Device entries: average draw while on, hours per day, usual start hour, shiftable.
    double Watts = 0, double HoursPerDay = 24, int? UsualFromHour = null, bool Shiftable = false,
    string Note = "")
{
    public Appliance ToAppliance(int sortOrder) => new()
    {
        Name = Name, Icon = Icon, KwhPerRun = KwhPerRun, DurationHours = DurationHours, RunsPerWeek = RunsPerWeek, SortOrder = sortOrder,
    };

    public Device ToDevice(int sortOrder) => new()
    {
        Name = Name, Icon = Icon, CatalogId = Id, Watts = Watts, HoursPerDay = HoursPerDay,
        UsualFromHour = UsualFromHour, Shiftable = Shiftable, SortOrder = sortOrder,
    };
}

public static class DeviceCatalog
{
    public static readonly IReadOnlyList<CatalogEntry> All =
    [
        // Machines that run a cycle - these go on the front page with a start button.
        new("vaskemaskine", "Vask og opvask", "🧺", "Vaskemaskine", true, KwhPerRun: 1.0, DurationHours: 2, RunsPerWeek: 4, Note: "40 °C bomuld ~0,7 kWh, 60 °C ~1,2 kWh"),
        new("toerretumbler", "Vask og opvask", "🌀", "Tørretumbler (kondens)", true, KwhPerRun: 2.5, DurationHours: 1.5, RunsPerWeek: 2),
        new("toerretumbler-vp", "Vask og opvask", "🌀", "Tørretumbler (varmepumpe)", true, KwhPerRun: 1.2, DurationHours: 2.5, RunsPerWeek: 2),
        new("opvaskemaskine", "Vask og opvask", "🍽️", "Opvaskemaskine", true, KwhPerRun: 1.2, DurationHours: 2, RunsPerWeek: 5, Note: "Eco-program ~0,8 kWh"),
        new("ovn", "Køkken", "🔥", "Ovn (1 time)", true, KwhPerRun: 1.5, DurationHours: 1, RunsPerWeek: 4),
        new("kogeplade", "Køkken", "🍳", "Kogeplade/induktion (aftensmad)", true, KwhPerRun: 0.6, DurationHours: 0.5, RunsPerWeek: 7),
        new("airfryer", "Køkken", "🍟", "Airfryer", true, KwhPerRun: 0.5, DurationHours: 0.5, RunsPerWeek: 3),
        new("mikroovn", "Køkken", "📦", "Mikroovn", true, KwhPerRun: 0.15, DurationHours: 0.25, RunsPerWeek: 7),
        new("elkedel", "Køkken", "🫖", "Elkedel (1 liter)", true, KwhPerRun: 0.12, DurationHours: 0.25, RunsPerWeek: 14),
        new("kaffemaskine", "Køkken", "☕", "Kaffemaskine", true, KwhPerRun: 0.15, DurationHours: 0.25, RunsPerWeek: 7),
        new("broedrister", "Køkken", "🍞", "Brødrister", true, KwhPerRun: 0.05, DurationHours: 0.25, RunsPerWeek: 7),
        new("stoevsuger", "Rengøring", "🧹", "Støvsuger", true, KwhPerRun: 0.4, DurationHours: 0.5, RunsPerWeek: 2),
        new("haartoerrer", "Bad", "💇", "Hårtørrer", true, KwhPerRun: 0.15, DurationHours: 0.25, RunsPerWeek: 5),
        new("elbil", "Transport", "🚗", "Elbil – opladning hjemme", true, KwhPerRun: 30, DurationHours: 4, RunsPerWeek: 2, Note: "Ret kWh til hvad bilen typisk får pr. ladning"),
        new("elcykel", "Transport", "🚲", "Elcykel – opladning", true, KwhPerRun: 0.5, DurationHours: 4, RunsPerWeek: 3),

        // Devices that just draw power. Watts = average draw while on.
        new("koeleskab", "Køl og frys", "🧊", "Køleskab", false, Watts: 17, Note: "≈150 kWh/år"),
        new("koelefryseskab", "Køl og frys", "🧊", "Køle-/fryseskab", false, Watts: 29, Note: "≈250 kWh/år"),
        new("fryser", "Køl og frys", "❄️", "Fryser (kumme/skab)", false, Watts: 25, Note: "≈220 kWh/år"),
        new("vinkoeleskab", "Køl og frys", "🍷", "Vinkøleskab", false, Watts: 12, Note: "≈100 kWh/år"),
        new("ekstra-koeleskab", "Køl og frys", "🧊", "Ekstra køleskab (garage/bryggers)", false, Watts: 30, Note: "Ældre skabe bruger ofte 250–350 kWh/år"),

        new("varmepumpe-luft-luft", "Varme og vand", "♨️", "Varmepumpe luft/luft (fyringssæson)", false, Watts: 800, HoursPerDay: 10, UsualFromHour: 6, Shiftable: true, Note: "Regn kun med fyringssæsonen"),
        new("varmepumpe-vand", "Varme og vand", "♨️", "Varmepumpe luft/vand eller jord (fyringssæson)", false, Watts: 1500, HoursPerDay: 10, UsualFromHour: 5, Shiftable: true),
        new("elradiator", "Varme og vand", "🌡️", "Elradiator / elpanel", false, Watts: 1000, HoursPerDay: 6, UsualFromHour: 16, Shiftable: true),
        new("gulvvarme-el", "Varme og vand", "🦶", "Elgulvvarme (badeværelse)", false, Watts: 300, HoursPerDay: 8, UsualFromHour: 5, Shiftable: true),
        new("varmtvandsbeholder", "Varme og vand", "🚿", "Elvandvarmer / varmtvandsbeholder", false, Watts: 2000, HoursPerDay: 2, UsualFromHour: 6, Shiftable: true, Note: "Kan ofte flyttes til natten med et ur"),
        new("cirkulationspumpe", "Varme og vand", "💧", "Cirkulationspumpe", false, Watts: 25),
        new("ventilation", "Varme og vand", "🌬️", "Ventilationsanlæg", false, Watts: 35),
        new("spa", "Varme og vand", "🛁", "Spabad / pool-pumpe", false, Watts: 400, HoursPerDay: 6, UsualFromHour: 10, Shiftable: true),

        new("tv", "Stue", "📺", "TV", false, Watts: 90, HoursPerDay: 4, UsualFromHour: 18),
        new("tv-standby", "Stue", "📺", "TV/boks standby", false, Watts: 8),
        new("spillekonsol", "Stue", "🎮", "Spillekonsol", false, Watts: 120, HoursPerDay: 2, UsualFromHour: 19),
        new("belysning", "Stue", "💡", "Belysning (hele boligen, LED)", false, Watts: 60, HoursPerDay: 5, UsualFromHour: 17),
        new("belysning-gl", "Stue", "💡", "Belysning (ældre pærer/halogen)", false, Watts: 250, HoursPerDay: 5, UsualFromHour: 17),

        new("computer", "Kontor og net", "🖥️", "Stationær computer", false, Watts: 150, HoursPerDay: 4, UsualFromHour: 9),
        new("gaming-pc", "Kontor og net", "🖥️", "Gaming-pc", false, Watts: 350, HoursPerDay: 3, UsualFromHour: 19),
        new("baerbar", "Kontor og net", "💻", "Bærbar computer", false, Watts: 30, HoursPerDay: 5, UsualFromHour: 9),
        new("skaerm", "Kontor og net", "🖥️", "Ekstra skærm", false, Watts: 30, HoursPerDay: 5, UsualFromHour: 9),
        new("router", "Kontor og net", "📶", "Router / modem / mesh", false, Watts: 12),
        new("nas", "Kontor og net", "💾", "NAS / hjemmeserver", false, Watts: 40),
        new("printer", "Kontor og net", "🖨️", "Printer (standby)", false, Watts: 4),
        new("alarm", "Kontor og net", "📷", "Alarm / kameraer / smart home", false, Watts: 10),
        new("opladere", "Kontor og net", "🔋", "Telefon- og tabletopladere", false, Watts: 10, HoursPerDay: 3, UsualFromHour: 22),

        new("robotstoevsuger", "Rengøring", "🤖", "Robotstøvsuger (inkl. dock)", false, Watts: 15, HoursPerDay: 24),
        new("akvarie", "Andet", "🐟", "Akvarie", false, Watts: 50),
        new("standby", "Andet", "🔌", "Diverse standby (hele boligen)", false, Watts: 20, Note: "Typisk 100–300 kWh/år i alt"),
    ];

    public static CatalogEntry? Find(string id) => All.FirstOrDefault(e => e.Id == id);
}
