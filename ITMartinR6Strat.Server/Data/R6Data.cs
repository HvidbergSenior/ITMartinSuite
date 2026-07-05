namespace ITMartinR6Strat.Server.Data;

public sealed record R6Map(string Id, string Name, string[] Sites);

public sealed record R6Operator(string Id, string Name, string Side)
{
    // Community CDN: https://r6operators.marcopixel.eu/icons/png/{id}.png
    public string ImageUrl => $"https://r6operators.marcopixel.eu/icons/png/{Id}.png";
}

public static class R6Data
{
    public static readonly R6Map[] Maps =
    [
        new("clubhouse",    "Clubhouse",           ["Bar/CCTV Room", "Cash Room/Gym", "Church/Arsenal Room"]),
        new("border",       "Border",              ["Supply Room/Tellers Room", "Armory Lockers/CCTV Room"]),
        new("kafe",         "Kafe Dostoyevsky",    ["Reading Room/Fireplace Hall", "Bar/Cocktail Lounge", "Freezer/Kitchen"]),
        new("coastline",    "Coastline",           ["Blue Bar/Sunrise Bar", "Kitchen/Service Entrance", "Theater/Penthouse"]),
        new("oregon",       "Oregon",              ["Kids Dorm/Master Dorm", "Meeting Hall/Kitchen", "Basement/Laundry"]),
        new("consulate",    "Consulate",           ["Consul Office/Lobby", "Garage/Tellers Room"]),
        new("bank",         "Bank",                ["CEO Office/Executive Lounge", "Open Area/Tellers Room", "Basement/Lockers"]),
        new("villa",        "Villa",               ["Aviator/Games Room", "Trophy/Dining Room", "Living Room/Kitchen"]),
        new("chalet",       "Chalet",              ["Map Room/Office", "Wine Cellar/Storage", "Bedroom/Snowmobile Garage"]),
        new("skyscraper",   "Skyscraper",          ["CEO Office/Boardroom", "Tea Room/Work Space"]),
        new("emeraldplains","Emerald Plains",      ["Bar/Billiard Room", "Master Bedroom/Office"]),
        new("nighthaven",   "Nighthaven Labs",     ["Server Room/Research Lab", "Control Room/Workshop"]),
        new("themepark",    "Theme Park",          ["Throne Room/Armory", "Bunk Room/Labs"]),
        new("outback",      "Outback",             ["Laundry/Supply Room", "President Suite/Office"]),
        new("fortress",     "Fortress",            ["Commander's Office/Archives", "Barracks/Dormitory"]),
    ];

    public static readonly R6Operator[] Attackers =
    [
        new("thermite",   "Thermite",  "attack"),
        new("ash",        "Ash",       "attack"),
        new("sledge",     "Sledge",    "attack"),
        new("thatcher",   "Thatcher",  "attack"),
        new("twitch",     "Twitch",    "attack"),
        new("hibana",     "Hibana",    "attack"),
        new("jackal",     "Jackal",    "attack"),
        new("ying",       "Ying",      "attack"),
        new("zofia",      "Zofia",     "attack"),
        new("lion",       "Lion",      "attack"),
        new("finka",      "Finka",     "attack"),
        new("maverick",   "Maverick",  "attack"),
        new("gridlock",   "Gridlock",  "attack"),
        new("amaru",      "Amaru",     "attack"),
        new("ace",        "Ace",       "attack"),
        new("zero",       "Zero",      "attack"),
        new("flores",     "Flores",    "attack"),
        new("buck",       "Buck",      "attack"),
        new("kali",       "Kali",      "attack"),
        new("dokkaebi",   "Dokkaebi",  "attack"),
        new("sens",       "Sens",      "attack"),
        new("brava",      "Brava",     "attack"),
        new("ram",        "Ram",       "attack"),
    ];

    public static readonly R6Operator[] Defenders =
    [
        new("rook",        "Rook",       "defence"),
        new("bandit",      "Bandit",     "defence"),
        new("jager",       "Jäger",      "defence"),
        new("kapkan",      "Kapkan",     "defence"),
        new("echo",        "Echo",       "defence"),
        new("pulse",       "Pulse",      "defence"),
        new("smoke",       "Smoke",      "defence"),
        new("frost",       "Frost",      "defence"),
        new("valkyrie",    "Valkyrie",   "defence"),
        new("ela",         "Ela",        "defence"),
        new("maestro",     "Maestro",    "defence"),
        new("vigil",       "Vigil",      "defence"),
        new("lesion",      "Lesion",     "defence"),
        new("mozzie",      "Mozzie",     "defence"),
        new("warden",      "Warden",     "defence"),
        new("oryx",        "Oryx",       "defence"),
        new("melusi",      "Melusi",     "defence"),
        new("thunderbird", "Thunderbird","defence"),
        new("azami",       "Azami",      "defence"),
        new("aruni",       "Aruni",      "defence"),
        new("thorn",       "Thorn",      "defence"),
        new("tubarao",     "Tubarão",    "defence"),
        new("skopos",      "Skopos",     "defence"),
    ];

    public static R6Operator? FindOperator(string name) =>
        Attackers.Concat(Defenders)
            .FirstOrDefault(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase)
                              || string.Equals(o.Id, name, StringComparison.OrdinalIgnoreCase));

    public static R6Map? FindMap(string id) =>
        Maps.FirstOrDefault(m => m.Id == id);

    public static string MapName(string id) =>
        Maps.FirstOrDefault(m => m.Id == id)?.Name ?? id;
}
