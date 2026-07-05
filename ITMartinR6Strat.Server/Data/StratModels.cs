namespace ITMartinR6Strat.Server.Data;

public enum StratStep
{
    WaitingMap,     // host must tap which map was selected
    WaitingSide,    // host taps Attack or Defence
    BanPhase,       // show ban suggestion, host taps site when ready
    PickPhase,      // show role cards, players pick operators
}

public sealed class StratSession
{
    public string   Code        { get; init; } = "";
    public string   HostToken   { get; init; } = "";
    public string[] MapPool     { get; set; }  = [];
    public StratStep Step       { get; set; }  = StratStep.WaitingMap;
    public string?  SelectedMap { get; set; }
    public string?  Side        { get; set; }  // "attack" | "defence"
    public string?  SelectedSite{ get; set; }
    public GeneratedPlan? Plan  { get; set; }
    public List<PlayerPick> Picks { get; } = [];
    public bool     Generating  { get; set; }
    public DateTime CreatedAt   { get; } = DateTime.UtcNow;

    // Cache site plans keyed by site name (pre-generated when side is set)
    public Dictionary<string, GeneratedPlan> SitePlanCache { get; } = new();
}

public sealed class GeneratedPlan
{
    public string         Strategy      { get; set; } = "";
    public BanCard?       Ban           { get; set; }
    public List<RoleCard> Roles         { get; set; } = [];
}

public sealed class RoleCard
{
    public string       Name       { get; set; } = "";
    public string       Emoji      { get; set; } = "";
    public string       Color      { get; set; } = "blue";
    public string       Task       { get; set; } = "";
    public List<string> Operators  { get; set; } = [];
    public List<string> Walls      { get; set; } = [];    // reinforce (defence) or breach (attack)
    public string       Rotation   { get; set; } = "";    // rotate route (defence) or approach (attack)
}

public sealed class BanCard
{
    public string Operator { get; set; } = "";
    public string Reason   { get; set; } = "";
    public string Alternate{ get; set; } = "";
}

public sealed class PlayerPick
{
    public string PlayerToken { get; set; } = "";
    public int    RoleIndex   { get; set; }
    public string OperatorId  { get; set; } = "";
}
