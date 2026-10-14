namespace ITMartinR6Assistant.Domain;

// One match read from Siege's own replay files (MatchReplay\Match-...\*.rec) by r6-dissect on a player's PC.
// Some rounds cannot be read (the tool crashes on them), so RoundsRead can be lower than RoundsTotal; the score
// still comes out right because every round carries the running score. Several teammates may send the same
// match - rounds are merged by number (see Merge).
public class ReplayMatch
{
    public string MatchId { get; set; } = "";
    public DateTime PlayedAtUtc { get; set; }
    public string Map { get; set; } = "";
    public string MatchType { get; set; } = "";
    public int OurScore { get; set; }
    public int TheirScore { get; set; }
    public int RoundsTotal { get; set; }
    // The round the score above is from. When the last rounds could not be read, InferFinalScore fills in the
    // final score if the round count makes it certain; otherwise ScoreComplete stays false and Won is null.
    public int ScoreRound { get; set; }
    public bool ScoreComplete { get; set; }
    public string SentBy { get; set; } = "";
    public List<ReplayRound> Rounds { get; set; } = new();

    public int RoundsRead => Rounds.Count;
    public bool? Won => !ScoreComplete || OurScore == TheirScore ? null : OurScore > TheirScore;

    // Ranked/unranked: first to 4, at 3-3 overtime to 5 - so N rounds in total means 4-0..4-3, 5-3 or 5-4.
    // Knowing the score after round k and N, the winner is certain when only one side can still reach the final score.
    public void InferFinalScore()
    {
        if (ScoreRound >= RoundsTotal) { ScoreComplete = true; return; }
        ScoreComplete = false;
        if (MatchType.Contains("Quick", StringComparison.OrdinalIgnoreCase) || RoundsTotal < 4 || RoundsTotal > 9) return;
        var win = RoundsTotal <= 7 ? 4 : 5;
        var lose = RoundsTotal - win;
        var weCan = OurScore <= win && TheirScore <= lose;
        var theyCan = TheirScore <= win && OurScore <= lose;
        if (weCan == theyCan) return;
        (OurScore, TheirScore) = weCan ? (win, lose) : (lose, win);
        ScoreComplete = true;
    }

    // Adds rounds the other copy has and this one lacks, and keeps the most complete score.
    public void Merge(ReplayMatch other)
    {
        foreach (var r in other.Rounds.Where(r => Rounds.All(x => x.Number != r.Number)))
            Rounds.Add(r);
        Rounds.Sort((a, b) => a.Number.CompareTo(b.Number));
        RoundsTotal = Math.Max(RoundsTotal, other.RoundsTotal);
        if (other.ScoreComplete && !ScoreComplete || other.ScoreRound > ScoreRound && !ScoreComplete)
        { OurScore = other.OurScore; TheirScore = other.TheirScore; ScoreRound = other.ScoreRound; ScoreComplete = other.ScoreComplete; }
        InferFinalScore();
        if (string.IsNullOrEmpty(Map)) Map = other.Map;
    }

    // Kills/deaths/assists per player over the rounds that could be read.
    public List<ReplayPlayerTotal> PlayerTotals() =>
        Rounds.SelectMany(r => r.Players)
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ReplayPlayerTotal(
                g.Key, g.First().OurTeam, g.Count(), g.Sum(p => p.Kills), g.Count(p => p.Died), g.Sum(p => p.Assists), g.Sum(p => p.Headshots),
                g.Select(p => p.Operator).Where(o => o.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList()))
            .OrderByDescending(p => p.OurTeam).ThenByDescending(p => p.Kills).ThenBy(p => p.Deaths)
            .ToList();
}

public class ReplayRound
{
    public int Number { get; set; }             // 1-based
    public string Side { get; set; } = "";      // our side: "Attack" | "Defense"
    public string Site { get; set; } = "";      // site name as in R6GameData when it could be matched, else the game's text
    public bool Won { get; set; }
    public string WinCondition { get; set; } = "";
    public List<ReplayPlayerRound> Players { get; set; } = new();
    public List<ReplayKill> Kills { get; set; } = new();
}

public class ReplayPlayerRound
{
    public string Name { get; set; } = "";
    public bool OurTeam { get; set; }
    public string Operator { get; set; } = "";
    public int Kills { get; set; }
    public bool Died { get; set; }
    public int Assists { get; set; }
    public int Headshots { get; set; }
}

public class ReplayKill
{
    public string Killer { get; set; } = "";
    public string Target { get; set; } = "";
    public string KillerOperator { get; set; } = "";
    public string TargetOperator { get; set; } = "";
    public bool Headshot { get; set; }
    public int TimeSeconds { get; set; }        // round clock when it happened (counts down)
    public bool KillerOurs { get; set; }
}

public sealed record ReplayPlayerTotal(string Name, bool OurTeam, int Rounds, int Kills, int Deaths, int Assists, int Headshots, List<string> Operators)
{
    public string Kd => Deaths == 0 ? Kills.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) : ((double)Kills / Deaths).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
    public int HeadshotPct => Kills > 0 ? 100 * Headshots / Kills : 0;
}

// Totals over all imported matches (user: "our stats on K/D and also for the operators").
public static class ReplayStats
{
    public static List<ReplayPlayerTotal> Players(IEnumerable<ReplayMatch> matches, bool ourTeam = true) =>
        matches.SelectMany(m => m.Rounds).SelectMany(r => r.Players).Where(p => p.OurTeam == ourTeam)
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ReplayPlayerTotal(g.Key, ourTeam, g.Count(), g.Sum(p => p.Kills), g.Count(p => p.Died), g.Sum(p => p.Assists), g.Sum(p => p.Headshots),
                g.GroupBy(p => p.Operator, StringComparer.OrdinalIgnoreCase).Where(o => o.Key.Length > 0).OrderByDescending(o => o.Count()).Select(o => o.Key).Take(3).ToList()))
            .OrderByDescending(p => p.Rounds).ThenByDescending(p => p.Kills)
            .ToList();

    // Per operator on one team: rounds played (one per player-round), K/D and how many of those rounds the team won.
    public static List<ReplayOperatorTotal> Operators(IEnumerable<ReplayMatch> matches, bool ourTeam = true, string? map = null) =>
        matches.Where(m => map is null || m.Map.Equals(map, StringComparison.OrdinalIgnoreCase))
            .SelectMany(m => m.Rounds)
            .SelectMany(r => r.Players.Where(p => p.OurTeam == ourTeam && p.Operator.Length > 0).Select(p => (r, p)))
            .GroupBy(x => x.p.Operator, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ReplayOperatorTotal(g.Key, g.First().r.Side == "Attack" == ourTeam ? "Attack" : "Defense",
                g.Count(), g.Sum(x => x.p.Kills), g.Count(x => x.p.Died), g.Count(x => x.r.Won == ourTeam)))
            .OrderByDescending(o => o.Rounds).ThenByDescending(o => o.Kills)
            .ToList();

    // Each of our players' operators with kills/deaths (user: "see which operators we kill with").
    public static List<ReplayOperatorTotal> PlayerOperators(IEnumerable<ReplayMatch> matches, string player) =>
        matches.SelectMany(m => m.Rounds)
            .SelectMany(r => r.Players.Where(p => p.OurTeam && p.Operator.Length > 0 && p.Name.Equals(player, StringComparison.OrdinalIgnoreCase)).Select(p => (r, p)))
            .GroupBy(x => x.p.Operator, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ReplayOperatorTotal(g.Key, g.First().r.Side, g.Count(), g.Sum(x => x.p.Kills), g.Count(x => x.p.Died), g.Count(x => x.r.Won)))
            .OrderByDescending(o => o.Kills).ThenByDescending(o => o.Rounds)
            .ToList();

    // Enemy operators that killed us most (and how often we killed them) - "who hurts us".
    public static List<(string Operator, int KilledUs, int WeKilled)> Threats(IEnumerable<ReplayMatch> matches, string? map = null)
    {
        var kills = matches.Where(m => map is null || m.Map.Equals(map, StringComparison.OrdinalIgnoreCase)).SelectMany(m => m.Rounds).SelectMany(r => r.Kills).ToList();
        return kills.Where(k => !k.KillerOurs && k.KillerOperator.Length > 0).GroupBy(k => k.KillerOperator, StringComparer.OrdinalIgnoreCase)
            .Select(g => (g.Key, g.Count(), kills.Count(k => k.KillerOurs && k.TargetOperator.Equals(g.Key, StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(x => x.Item2).ToList();
    }
}

public sealed record ReplayOperatorTotal(string Operator, string Side, int Rounds, int Kills, int Deaths, int RoundsWon)
{
    public string Kd => Deaths == 0 ? Kills.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) : ((double)Kills / Deaths).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
    public int WinPct => Rounds > 0 ? 100 * RoundsWon / Rounds : 0;
}
