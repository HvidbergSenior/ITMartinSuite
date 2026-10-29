namespace ITMartinStrategi.Domain;

/// <summary>One Wingspan bird as the card data has it (tools/strategi/wingspan_lookup.js from the Wingsearch data).
/// Kind = what its power does: egg, tuck, cache, cards, food, pink, predator, ... Color = when it works: brown (when
/// activated), white (when played), yellow (game end), pink (between turns), teal (round end).</summary>
public sealed record WingspanBird(
    string Name, string Tier, double Score, IReadOnlyList<string> Habitats, IReadOnlyDictionary<string, int> Food, bool FoodOr,
    int Points, int Eggs, string Nest, string Color, string Kind, string Power, string Why)
{
    /// <summary>Food to pay for it: one of them when FoodOr, else all.</summary>
    public int FoodCost => FoodOr ? Math.Min(1, Food.Values.Sum()) : Food.Values.Sum();
}

/// <summary>One try at a week's challenge (user 2026-10-29: "i want to make strategies on this challenge").</summary>
public sealed class ChallengeAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime PlayedUtc { get; set; } = DateTime.UtcNow;
    public int Score { get; set; }
    public string Note { get; set; } = "";
}

/// <summary>A week's "Champ of the Birds" challenge against the Automa: the same deck for everyone, so the birds are
/// written down once and every attempt builds on the last. Birds = the names as typed (start hand first, then the
/// ones seen along the way).</summary>
public sealed class WingspanChallenge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Week { get; set; } = "";
    public List<string> StartBirds { get; set; } = [];
    public List<string> SeenBirds { get; set; } = [];
    public string Note { get; set; } = "";
    public List<ChallengeAttempt> Attempts { get; set; } = [];
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public ChallengeAttempt? Best => Attempts.MaxBy(a => a.Score);
}

/// <summary>What to do with one bird this week.</summary>
public sealed record BirdAdvice(WingspanBird Bird, string Role, string When, string Note);

/// <summary>The plan for a week: the row to build in, the birds in play order, and the combos between this week's birds.</summary>
public sealed record ChallengePlan(string Row, string RowWhy, IReadOnlyList<BirdAdvice> Order, IReadOnlyList<string> Combos);

/// <summary>Reads a week's birds and says where the points are (no AI - user: no cost). Rules of thumb from the guides:
/// an engine early (cheap brown birds in ONE row), food birds before the birds they pay for, points and game-end birds late.</summary>
public static class ChallengeAdvisor
{
    private static readonly string[] Rows = ["forest", "grassland", "wetland"];

    public static ChallengePlan Plan(IReadOnlyList<WingspanBird> birds)
    {
        var row = BestRow(birds);
        var order = birds.Select(b => Advise(b, row)).OrderBy(a => Rank(a.Bird, row)).ThenByDescending(a => a.Bird.Score).ToList();
        return new ChallengePlan(row, RowWhy(birds, row), order, Combos(birds, row));
    }

    // The row where this week's brown (engine) birds can sit together - a tucking bird counts double, it scores per bird in its row.
    private static string BestRow(IReadOnlyList<WingspanBird> birds)
    {
        if (birds.Count == 0) return "forest";
        return Rows.MaxBy(r => birds.Where(b => b.Habitats.Contains(r))
            .Sum(b => (b.Color == "brown" ? 2 : 1) + (b.Kind == "tuck" && b.Color == "brown" ? 2 : 0) + b.Score / 10))!;
    }

    private static string RowWhy(IReadOnlyList<WingspanBird> birds, string row)
    {
        var fit = birds.Where(b => b.Habitats.Contains(row)).Select(b => b.Name).ToList();
        var engine = birds.Where(b => b.Color == "brown" && b.Habitats.Contains(row)).Select(b => b.Name).ToList();
        var action = row switch { "forest" => "gain food", "grassland" => "lay eggs", _ => "draw cards" };
        return $"{fit.Count} of this week's {birds.Count} birds can live there ({string.Join(", ", fit)}), and the row's action is to {action}."
               + (engine.Count > 0 ? $" Engine there: {string.Join(", ", engine)} – they work every time you use the row." : "");
    }

    // 0 = first: cheap food birds that pay for the others, then cheap engine birds in the row, then the rest, game end last.
    private static int Rank(WingspanBird b, string row) => b switch
    {
        { Color: "white", Kind: "food" } when b.FoodCost <= 1 => 0,
        { Color: "brown" } when b.Habitats.Contains(row) && b.FoodCost <= 2 => 1,
        { Color: "brown" } or { Color: "pink" } => 2,
        { Color: "yellow" } => 4,
        _ => 3,
    };

    private static BirdAdvice Advise(WingspanBird b, string row)
    {
        var inRow = b.Habitats.Contains(row);
        return b switch
        {
            { Color: "white", Kind: "food" } when b.FoodCost <= 1 =>
                new(b, "Food burst", "Turn 1-2", $"Cheap, and its food pays for the next birds. {Cap(b.Why)}."),
            { Color: "brown", Kind: "tuck" } =>
                new(b, "Engine – points", inRow ? $"Early, in {row}" : "Early",
                    $"Every bird you add to its row is +1 point per activation – keep cards in hand to tuck. {Cap(b.Why)}."),
            { Color: "brown" } =>
                new(b, "Engine", inRow ? $"Early, in {row}" : $"When you have the food ({b.FoodCost})",
                    $"{Cap(b.Why)}." + (b.FoodCost >= 3 ? " Costs 3 food – not before your food birds have paid for it." : "")),
            { Color: "yellow" } =>
                new(b, "Game end", "Round 3-4", $"Scores at the end – play it late on a full board. {Cap(b.Why)}."),
            { Color: "white" } when b.Points >= 6 =>
                new(b, "Points", "Round 3-4", $"{b.Points} points; its power is small – play it when the engine is done. {Cap(b.Why)}."),
            _ => new(b, b.Kind, "When it fits", $"{Cap(b.Why)}."),
        };
    }

    private static string Cap(string s) => s.Length > 0 ? char.ToUpperInvariant(s[0]) + s[1..] : s;

    private static List<string> Combos(IReadOnlyList<WingspanBird> birds, string row)
    {
        var combos = new List<string>();
        foreach (var t in birds.Where(b => b.Kind == "tuck" && b.Color == "brown"))
        {
            // Its own row: the plan's row when it can live there, else the one of its habitats with most of this week's birds.
            var home = t.Habitats.Contains(row) ? row : t.Habitats.MaxBy(h => birds.Count(b => b.Habitats.Contains(h))) ?? row;
            var mates = birds.Where(b => b != t && b.Habitats.Contains(home)).Select(b => b.Name).ToList();
            if (mates.Count > 0) combos.Add($"{t.Name} + {string.Join(", ", mates)} together in {home}: one tucked card behind each = up to {mates.Count + 1} points per activation.");
            if (!birds.Any(b => b.Kind == "cards")) combos.Add($"{t.Name} needs cards in hand – use the wetland (draw cards) between activations, nothing this week draws for you.");
        }
        foreach (var f in birds.Where(b => b.Kind == "food" && b.FoodCost <= 1))
        {
            var foods = new[] { "invertebrate", "seed", "fish", "fruit", "rodent", "nectar" }.Where(x => f.Power.Contains(x)).ToList();
            var fed = birds.Where(b => b != f && b.Food.Keys.Intersect(foods).Any()).Select(b => b.Name).ToList();
            if (fed.Count > 0) combos.Add($"{f.Name} gets {string.Join("/", foods)} – exactly what {string.Join(", ", fed)} need. Play it first.");
        }
        foreach (var e in birds.Where(b => b.Power.Contains("star] nest") || b.Power.Contains("star nest")))
        {
            var stars = birds.Where(b => b != e && b.Nest == "wild").Select(b => b.Name).ToList();
            combos.Add(stars.Count > 0
                ? $"{e.Name} lays eggs on star-nest birds: {string.Join(", ", stars)} + itself."
                : $"{e.Name} only lays on star-nest birds – this week only on itself, so 1 egg. Look for star nests in the tray.");
        }
        if (birds.Any(b => b.Power.Contains("neighbor")))
            combos.Add("Powers that take from neighbours hit the Automa – check in the game how much it gives back.");
        return combos;
    }
}
