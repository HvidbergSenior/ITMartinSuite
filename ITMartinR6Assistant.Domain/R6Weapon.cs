namespace ITMartinR6Assistant.Domain;

// Siege weapon stats from the wiki's "Siege" tab, plus a tier computed within the weapon class.
public class R6Weapon
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";       // "Assault Rifle", "Submachine Gun", "Shotgun", ...
    public int Damage { get; set; }               // per hit (per pellet for shotguns)
    public int Rpm { get; set; }
    public int Mobility { get; set; }
    public int Magazine { get; set; }
    public double ReloadTactical { get; set; }    // seconds, 0 = unknown
    public List<string> Users { get; set; } = new();

    // Time to kill a 3-armor operator (125 HP) with body shots, in milliseconds; 0 = not computed.
    public int TtkMs { get; set; }
    public int Dps { get; set; }
    public string Tier { get; set; } = "";
    public List<string> Strengths { get; set; } = new();
    public List<string> Weaknesses { get; set; } = new();
}
