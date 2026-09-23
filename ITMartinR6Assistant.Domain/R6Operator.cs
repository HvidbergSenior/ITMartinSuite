namespace ITMartinR6Assistant.Domain;

public class R6Operator
{
    public string Name { get; set; } = "";
    public string Side { get; set; } = ""; // "Attack" | "Defense"
    public string Role { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    // One plain-Danish line on what the gadget does to the other team - used as the ban reason.
    public string Threat { get; set; } = "";

    // Fetched from Liquipedia by "Hent nyeste data" (free, no AI).
    public int Armor { get; set; }   // 1-3
    public int Speed { get; set; }   // 1-3
    public List<string> Primaries { get; set; } = new();
    public List<string> Secondaries { get; set; } = new();
    public List<string> Gadgets { get; set; } = new();
    public string Ability { get; set; } = "";
    // Operators whose gadgets counter this one (Liquipedia's gadget card).
    public List<string> CounteredBy { get; set; } = new();

    // Computed from the numbers: tier within the side and the reasons behind it.
    public string Tier { get; set; } = "";
    public List<string> TierWhy { get; set; } = new();
}
