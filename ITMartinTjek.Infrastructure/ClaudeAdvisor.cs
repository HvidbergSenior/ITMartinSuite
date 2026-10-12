using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using ITMartinTjek.Application;
using ITMartinTjek.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ITMartinTjek.Infrastructure;

/// <summary>"Hvad er galt?" - Claude gets the check results (browser + optional deep script)
/// and the user's own description, and answers with what is fine, what is wrong and
/// concrete steps in plain Danish. The day's budget is taken by the use case (Diagnosis) before this is called.</summary>
public sealed class ClaudeAdvisor : IAdvisor
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly AnthropicClient? _client;
    private readonly ILogger<ClaudeAdvisor> _logger;

    public bool Enabled => _client is not null;

    public ClaudeAdvisor(IConfiguration config, ILogger<ClaudeAdvisor> logger)
    {
        var apiKey = config["Claude:ApiKey"];
        _client = string.IsNullOrWhiteSpace(apiKey) ? null : new AnthropicClient { ApiKey = apiKey };
        _logger = logger;
    }

    private static JsonElement P(string type, string description, string? items = null) =>
        JsonDocument.Parse(items is null
            ? $$"""{"type":"{{type}}","description":"{{description}}"}"""
            : $$"""{"type":"array","items":{"type":"{{items}}"},"description":"{{description}}"}""").RootElement;

    private static readonly Tool SvarTool = new()
    {
        Name = "svar",
        Description = "Dit svar til brugeren, på dansk",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["diagnose"] = P("string", "1-3 sætninger: hvad er mest sandsynligt galt (eller at alt ser fint ud)"),
                ["fint"] = P("array", "Det der er i orden og ikke skal røres", "string"),
                ["problemer"] = P("array", "Konkrete fund der forklarer symptomet, vigtigste først", "string"),
                ["goerNu"] = P("array", "Trin brugeren skal gøre nu, i rækkefølge, med præcise klik/steder (Windows: Indstillinger > ...)", "string"),
                ["kanFjernes"] = P("array", "Ting der ser ubrugte/døde ud og er sikre at fjerne - tom hvis intet", "string"),
            },
            Required = ["diagnose", "fint", "problemer", "goerNu", "kanFjernes"],
        },
    };

    public async Task<Advice> AskAsync(string device, string browserJson, string? deepJson, string description, CancellationToken ct = default)
    {
        if (_client is null) throw new InvalidOperationException("Claude:ApiKey mangler");

        var prompt = $"""
            Enhed: {device}
            Brugerens beskrivelse af problemet: {(string.IsNullOrWhiteSpace(description) ? "(ingen - giv en generel vurdering)" : description)}

            Browser-tjek (JSON, status ok/warn/bad/info):
            {browserJson}

            {(deepJson is null ? "Dybt tjek: ikke kørt." : "Dybt tjek fra script på enheden (JSON):\n" + deepJson)}

            Kald svar-værktøjet.
            """;

        var request = new MessageCreateParams
        {
            Model = "claude-opus-5",
            MaxTokens = 2000,
            System = """
                Du er en rolig, erfaren IT-supporter der hjælper almindelige mennesker i Danmark med deres egen enhed
                (telefon, tablet eller computer). Du får resultatet af et automatisk tjek og evt. brugerens beskrivelse.
                Svar på dansk, konkret og uden fagjargon; forklar kort hvorfor når du beder om et trin.
                Gæt ikke ud over data: hvis tjekket ikke viser noget der forklarer symptomet, sig det og foreslå
                det mest sandsynlige næste skridt (fx genstart, kabel, opdatering, prøv en anden browser).
                Foreslå aldrig at slette noget der kan indeholde data; "kanFjernes" er kun til døde enheder, gamle
                driverpakker, ubrugte lydprogrammer og lignende.
                """,
            Tools = [SvarTool],
            ToolChoice = new ToolChoiceTool { Name = "svar" },
            Messages = [new() { Role = Role.User, Content = new List<ContentBlockParam> { new TextBlockParam { Text = prompt } } }],
        };

        var response = await _client.Messages.Create(request, ct);
        ToolUseBlock? toolUse = null;
        foreach (var block in response.Content)
            if (block.TryPickToolUse(out var tu)) { toolUse = tu; break; }
        if (toolUse is null) throw new InvalidOperationException("Intet svar");

        var json = JsonSerializer.Serialize(toolUse.Input);
        _logger.LogInformation("Tjek AI ({Device}, {In}/{Out} tokens): {Json}", device, response.Usage.InputTokens, response.Usage.OutputTokens, json);
        return JsonSerializer.Deserialize<Advice>(json, Json) ?? throw new InvalidOperationException("Kunne ikke læse svaret");
    }
}
