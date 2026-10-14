using System.Text.Json;
using System.Text.RegularExpressions;
using ITMartinR6Assistant.Application;
using ITMartinR6Assistant.Domain;

namespace ITMartinR6Assistant.Infrastructure;

// Reads the payload from wwwroot/scripts/R6Replays.ps1:
//   { "spiller": "...", "folder": "Match-...", "totalRounds": 9, "rounds": [ <r6-dissect JSON for one .rec>, ... ] }
// r6-dissect (github.com/redraskal/r6-dissect, v0.24.0) still reads Y11S3 replays but crashes on about a third of
// the rounds, so the script runs it per round file and sends the ones that worked.
// Facts that hold per round: team names "YOUR TEAM"/"ENEMY TEAM" (YOUR = the recording player's team),
// startingScore/score per team (the "won" flag is unreliable - both teams can say true), role Attack/Defense.
public sealed partial class R6DissectReplayParser : IReplayParser
{
    public ReplayMatch? Parse(string json, R6GameData data)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("rounds", out var roundsEl) || roundsEl.ValueKind != JsonValueKind.Array) return null;

        var match = new ReplayMatch
        {
            SentBy = Str(root, "spiller"),
            RoundsTotal = root.TryGetProperty("totalRounds", out var tr) && tr.TryGetInt32(out var n) ? n : 0,
        };
        string mapText = "";
        var replaySites = new List<string>();
        int lastNumber = -1;

        foreach (var r in roundsEl.EnumerateArray())
        {
            if (!r.TryGetProperty("teams", out var teamsEl) || teamsEl.GetArrayLength() < 2) continue;
            var teams = teamsEl.EnumerateArray().ToList();
            var ourIdx = teams.FindIndex(t => Str(t, "name").Equals("YOUR TEAM", StringComparison.OrdinalIgnoreCase));
            if (ourIdx < 0) ourIdx = RecorderTeam(r) ?? 0;
            var our = teams[ourIdx];
            var their = teams[1 - ourIdx];

            if (match.MatchId.Length == 0) match.MatchId = Str(r, "matchID");
            if (DateTime.TryParse(Str(r, "timestamp"), null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var ts)
                && (match.PlayedAtUtc == default || ts < match.PlayedAtUtc))
                match.PlayedAtUtc = ts;
            if (match.MatchType.Length == 0 && r.TryGetProperty("matchType", out var mt)) match.MatchType = Str(mt, "name");
            if (mapText.Length == 0 && r.TryGetProperty("map", out var mp)) mapText = Str(mp, "name");

            var number = (r.TryGetProperty("roundNumber", out var rn) && rn.TryGetInt32(out var rni) ? rni : 0) + 1;
            var ourWon = Int(our, "score") > Int(our, "startingScore");
            var site = Str(r, "site");
            replaySites.Add(site);

            var round = new ReplayRound
            {
                Number = number,
                Side = Str(our, "role"),
                Site = site,
                Won = ourWon,
                WinCondition = Str(ourWon ? our : their, "winCondition"),
            };

            // Players: team + operator; stats: kills/died/assists/headshots this round.
            var ops = new Dictionary<string, (bool Ours, string Op)>(StringComparer.OrdinalIgnoreCase);
            if (r.TryGetProperty("players", out var players) && players.ValueKind == JsonValueKind.Array)
                foreach (var p in players.EnumerateArray())
                {
                    var op = p.TryGetProperty("operator", out var o) ? Str(o, "name") : "";
                    ops[Str(p, "username")] = (Int(p, "teamIndex") == ourIdx, op);
                }
            if (r.TryGetProperty("stats", out var stats) && stats.ValueKind == JsonValueKind.Array)
                foreach (var s in stats.EnumerateArray())
                {
                    var name = Str(s, "username");
                    ops.TryGetValue(name, out var info);
                    round.Players.Add(new ReplayPlayerRound
                    {
                        Name = name, OurTeam = info.Ours, Operator = info.Op ?? "",
                        Kills = Int(s, "kills"), Died = s.TryGetProperty("died", out var d) && d.ValueKind == JsonValueKind.True,
                        Assists = Int(s, "assists"), Headshots = Int(s, "headshots"),
                    });
                }
            if (r.TryGetProperty("matchFeedback", out var feed) && feed.ValueKind == JsonValueKind.Array)
                foreach (var e in feed.EnumerateArray())
                {
                    if (!e.TryGetProperty("type", out var type) || Str(type, "name") != "Kill") continue;
                    var killer = Str(e, "username");
                    var target = Str(e, "target");
                    ops.TryGetValue(killer, out var k);
                    ops.TryGetValue(target, out var t);
                    round.Kills.Add(new ReplayKill
                    {
                        Killer = killer, Target = target, KillerOperator = k.Op ?? "", TargetOperator = t.Op ?? "",
                        Headshot = e.TryGetProperty("headshot", out var h) && h.ValueKind == JsonValueKind.True,
                        TimeSeconds = Int(e, "timeInSeconds"), KillerOurs = k.Ours,
                    });
                }

            if (match.Rounds.All(x => x.Number != number)) match.Rounds.Add(round);
            if (number > lastNumber)
            {
                lastNumber = number;
                match.OurScore = Int(our, "score");
                match.TheirScore = Int(their, "score");
                match.ScoreRound = number;
            }
        }

        if (match.Rounds.Count == 0) return null;
        match.Rounds.Sort((a, b) => a.Number.CompareTo(b.Number));
        if (match.MatchId.Length == 0) match.MatchId = Str(root, "folder");
        match.RoundsTotal = Math.Max(match.RoundsTotal, match.Rounds.Max(x => x.Number));
        match.InferFinalScore();

        var map = ResolveMap(mapText, replaySites, data);
        match.Map = map?.Name ?? mapText;
        if (map is not null)
            foreach (var round in match.Rounds)
                round.Site = BestSite(map, round.Site)?.Name ?? round.Site;
        return match;
    }

    private static int? RecorderTeam(JsonElement r)
    {
        if (!r.TryGetProperty("recordingPlayerID", out var idEl) || !r.TryGetProperty("players", out var players)) return null;
        var id = idEl.GetRawText();
        foreach (var p in players.EnumerateArray())
            if (p.TryGetProperty("id", out var pid) && pid.GetRawText() == id) return Int(p, "teamIndex");
        return null;
    }

    // r6-dissect has names only for older map ids ("Map(409325881472)" for reworked Villa), so the rooms of the
    // sites played decide the map: "2F Aviator Room, 2F Games Room" -> Villa's "Aviator Room / Games Room".
    private static R6Map? ResolveMap(string mapText, List<string> replaySites, R6GameData data)
    {
        var plain = Norm(mapText);
        if (plain.Length > 0 && !mapText.StartsWith("Map(", StringComparison.OrdinalIgnoreCase))
        {
            var byName = data.Maps.FirstOrDefault(m => Norm(m.Name) == plain)
                ?? data.Maps.FirstOrDefault(m => Norm(m.Name).StartsWith(plain) || plain.StartsWith(Norm(m.Name)));
            if (byName is not null) return byName;
        }
        return data.Maps
            .Select(m => (m, score: replaySites.Sum(s => BestSiteScore(m, s))))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .Select(x => x.m)
            .FirstOrDefault();
    }

    private static BombSite? BestSite(R6Map map, string replaySite) =>
        map.Sites.Select(s => (s, score: Overlap(s, replaySite))).Where(x => x.score > 0).OrderByDescending(x => x.score).Select(x => x.s).FirstOrDefault();

    private static int BestSiteScore(R6Map map, string replaySite) => map.Sites.Count == 0 ? 0 : map.Sites.Max(s => Overlap(s, replaySite));

    private static int Overlap(BombSite site, string replaySite)
    {
        var ours = site.Name.Split('/').Select(Room).ToHashSet();
        return replaySite.Split(',').Select(x => Room(FloorPrefix().Replace(x.Trim(), ""))).Count(ours.Contains);
    }

    // "Statuary Room" and "Statuary" are the same room; floors and punctuation do not matter.
    private static string Room(string s) => Norm(Regex.Replace(s, @"\broom\b", "", RegexOptions.IgnoreCase));
    private static string Norm(string s) => new(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    [GeneratedRegex(@"^(B|EXT|\d+F)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex FloorPrefix();

    private static string Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static int Int(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.TryGetInt32(out var i) ? i : 0;
}
