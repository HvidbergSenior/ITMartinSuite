namespace ITMartinR6Assistant.Domain;

public class BattlePlan
{
    public int Number { get; set; }
    public string Label { get; set; } = "";
    public List<string> Picks { get; set; } = new();
    public string Note { get; set; } = "";
}
