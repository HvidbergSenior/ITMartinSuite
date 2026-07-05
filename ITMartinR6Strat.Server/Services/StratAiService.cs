using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ITMartinR6Strat.Server.Data;

namespace ITMartinR6Strat.Server.Services;

public sealed class StratAiService(IHttpClientFactory http, IConfiguration config)
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };
    private readonly string? _apiKey = config["Claude:ApiKey"] ?? config["CLAUDE__APIKEY"];

    public async Task<GeneratedPlan> GeneratePlanAsync(string map, string side, string site, CancellationToken ct = default)
    {
        var isDefence          = side == "defence";
        var wallLabel          = isDefence ? "walls to reinforce (list only most critical, e.g. 'Bar north wall x2')" : "walls/surfaces to breach or destroy";
        var routeLabel         = isDefence ? "rotation route if overrun (e.g. 'Rotate via blue hallway to Bar')" : "approach route / entry direction";
        var opponentSide       = isDefence ? "attacker" : "defender";

        var prompt = $$"""
You are an R6 Siege tactical expert. Give a simple, effective strategy for an amateur to intermediate team.

Map: {{map}}
Side: {{side}}
Bomb site: {{site}}

Return ONLY valid JSON — no markdown, no explanation:
{
  "strategy": "2-sentence overview",
  "ban": {
    "operator": "most dangerous {{opponentSide}} on this map",
    "reason": "why (1 sentence)",
    "alternate": "second ban option"
  },
  "roles": [
    {
      "name": "ANKER",
      "emoji": "🏠",
      "color": "blue",
      "task": "what to do at round start (1 sentence, very specific)",
      "operators": ["Op1", "Op2"],
      "walls": ["{{wallLabel}}"],
      "rotation": "{{routeLabel}}"
    }
  ]
}

Give exactly 5 roles appropriate for {{side}}:
- Defence: 2 Anker (bomb room), 1 Roamer (1F pressure), 1 Support (cameras/gadgets), 1 Flankvagt (cut rotates)
- Attack: 1 Hard Breach, 1 Støtte (disable gadgets), 1 Entry, 1 Flankvagt (watch rotate), 1 Flex

For each role suggest 2 operators that fit best for this specific map and site.
For walls: be specific — name the room and which wall (north/south/window/floor etc).
For rotation: name the exact corridor or room to rotate through.
""";

        if (_apiKey is null)
            return FallbackPlan(map, side, site);

        try
        {
            var client = http.CreateClient("claude");
            var body = new
            {
                model = "claude-haiku-4-5-20251001",
                max_tokens = 1500,
                system = "You are an R6 Siege tactical expert. Return only valid JSON.",
                messages = new[] { new { role = "user", content = prompt } }
            };

            var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
            req.Headers.Add("x-api-key", _apiKey);
            req.Headers.Add("anthropic-version", "2023-06-01");
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            var resp = await client.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();

            var json = JsonNode.Parse(await resp.Content.ReadAsStringAsync(ct));
            var text = json?["content"]?[0]?["text"]?.GetValue<string>() ?? "";

            // Strip markdown code fences if present
            text = text.Trim();
            if (text.StartsWith("```")) text = text[(text.IndexOf('\n') + 1)..];
            if (text.EndsWith("```")) text  = text[..text.LastIndexOf("```")];

            var plan = JsonSerializer.Deserialize<AiPlanDto>(text.Trim(), _json);
            return MapPlan(plan);
        }
        catch
        {
            return FallbackPlan(map, side, site);
        }
    }

    private static GeneratedPlan MapPlan(AiPlanDto? dto)
    {
        if (dto is null) return FallbackPlan("", "", "");
        return new GeneratedPlan
        {
            Strategy = dto.Strategy ?? "",
            Ban = dto.Ban is null ? null : new BanCard
            {
                Operator  = dto.Ban.Operator  ?? "",
                Reason    = dto.Ban.Reason    ?? "",
                Alternate = dto.Ban.Alternate ?? ""
            },
            Roles = (dto.Roles ?? []).Select(r => new RoleCard
            {
                Name      = r.Name      ?? "",
                Emoji     = r.Emoji     ?? "🎯",
                Color     = r.Color     ?? "blue",
                Task      = r.Task      ?? "",
                Operators = r.Operators ?? [],
                Walls     = r.Walls     ?? [],
                Rotation  = r.Rotation  ?? ""
            }).ToList()
        };
    }

    private static GeneratedPlan FallbackPlan(string map, string side, string site) => new()
    {
        Strategy = $"Standard {side} setup for {map} — {site}.",
        Ban = new BanCard { Operator = "Ash", Reason = "High pick rate, versatile entry.", Alternate = "Thermite" },
        Roles =
        [
            new RoleCard { Name="ANKER",     Emoji="🏠", Color="blue",   Task="Hold bomb room from inside.",       Operators=["Rook","Echo"],    Walls=["Bomb room walls x2"], Rotation="Hold position" },
            new RoleCard { Name="ANKER",     Emoji="🏠", Color="blue",   Task="Hold second bomb room.",            Operators=["Bandit","Aruni"],  Walls=["Window wall"],         Rotation="Rotate via hallway" },
            new RoleCard { Name="ROAMER",    Emoji="🏃", Color="yellow", Task="Roam 1F, waste attacker time.",     Operators=["Jäger","Vigil"],  Walls=[],                      Rotation="Fall back early" },
            new RoleCard { Name="SUPPORT",   Emoji="🛡️", Color="green",  Task="Set up cameras and traps.",         Operators=["Valkyrie","Pulse"],Walls=[],                      Rotation="Feed intel to anchors" },
            new RoleCard { Name="FLANKVAGT", Emoji="👁️", Color="purple", Task="Hold flank/rotate route.",          Operators=["Frost","Kapkan"], Walls=[],                      Rotation="Stay until site needs help" },
        ]
    };

    // DTOs for deserialization
    private sealed class AiPlanDto
    {
        public string?        Strategy { get; set; }
        public AiBanDto?      Ban      { get; set; }
        public List<AiRoleDto>? Roles  { get; set; }
    }
    private sealed class AiBanDto
    {
        public string? Operator  { get; set; }
        public string? Reason    { get; set; }
        public string? Alternate { get; set; }
    }
    private sealed class AiRoleDto
    {
        public string?        Name      { get; set; }
        public string?        Emoji     { get; set; }
        public string?        Color     { get; set; }
        public string?        Task      { get; set; }
        public List<string>?  Operators { get; set; }
        public List<string>?  Walls     { get; set; }
        public string?        Rotation  { get; set; }
    }
}
