namespace ITMartinStarRealms.Server.Services;

public sealed record Faction(string Name, string Icon, string Color);

public sealed record Ship(string Name, string Faction);

public static class ShipCatalog
{
    public static readonly Faction[] Factions =
    [
        new("Trade Federation", "💚", "#2ecc71"),
        new("Blob",              "🟠", "#e67e22"),
        new("Star Empire",       "🟣", "#9b59b6"),
        new("Machine Cult",      "🔴", "#e74c3c"),
        new("Unaligned",         "⚪", "#95a5a6"),
    ];

    public static readonly Ship[] Ships =
    [
        // Trade Federation
        new("Federation Shuttle", "Trade Federation"),
        new("Cutter", "Trade Federation"),
        new("Embassy Yacht", "Trade Federation"),
        new("Trade Escort", "Trade Federation"),
        new("Flagship", "Trade Federation"),
        new("Command Ship", "Trade Federation"),
        new("Defense Center", "Trade Federation"),
        new("Port of Call", "Trade Federation"),
        new("Central Office", "Trade Federation"),
        new("Trading Post", "Trade Federation"),

        // Blob
        new("Blob Fighter", "Blob"),
        new("Trade Pod", "Blob"),
        new("Battle Blob", "Blob"),
        new("Blob Carrier", "Blob"),
        new("Blob Wheel", "Blob"),
        new("Blob World", "Blob"),
        new("The Hive", "Blob"),
        new("Ram", "Blob"),
        new("Battle Station", "Blob"),

        // Star Empire
        new("Survey Ship", "Star Empire"),
        new("Corvette", "Star Empire"),
        new("Dreadnaught", "Star Empire"),
        new("Imperial Fighter", "Star Empire"),
        new("Imperial Frigate", "Star Empire"),
        new("Recycling Station", "Star Empire"),
        new("Royal Redoubt", "Star Empire"),
        new("Fleet HQ", "Star Empire"),
        new("Space Station", "Star Empire"),

        // Machine Cult
        new("Missile Bot", "Machine Cult"),
        new("Missile Mech", "Machine Cult"),
        new("Battle Pod", "Machine Cult"),
        new("Junkyard", "Machine Cult"),
        new("Mothership", "Machine Cult"),
        new("Patrol Mech", "Machine Cult"),
        new("Stealth Needle", "Machine Cult"),
        new("Brain World", "Machine Cult"),
        new("Machine Base", "Machine Cult"),

        // Unaligned / explorer
        new("Explorer", "Unaligned"),
        new("Scout", "Unaligned"),
        new("Viper", "Unaligned"),
    ];

    public static string IconFor(string faction) =>
        Factions.FirstOrDefault(f => f.Name == faction)?.Icon ?? "❔";

    public static string ColorFor(string faction) =>
        Factions.FirstOrDefault(f => f.Name == faction)?.Color ?? "#95a5a6";
}
