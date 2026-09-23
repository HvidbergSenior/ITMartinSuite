namespace ITMartinR6Assistant.Domain;

public class R6GameData
{
    public List<R6Operator> Operators { get; set; } = new();
    public List<R6Map> Maps { get; set; } = new();

    // Filled by the "Opdater al info" research run, so the UI can say how fresh the data is.
    public string Season { get; set; } = "";
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public List<string> Sources { get; set; } = new();
    // Official ban rate in percent (the highest platform figure), used to rank bans and pick the "SKAL bannes" ones.
    public Dictionary<string, int> BanRates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    // Current tier per operator: "S", "A", "B" or "C".
    public Dictionary<string, string> Tiers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<R6Weapon> Weapons { get; set; } = new();
    // What changed in the last "Hent nyeste data" compared with the run before (loadouts, weapon stats, ban rates).
    public List<string> LastChanges { get; set; } = new();
}
