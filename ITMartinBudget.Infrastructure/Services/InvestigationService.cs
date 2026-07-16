using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Application.Models;
using Microsoft.Extensions.Configuration;

namespace ITMartinBudget.Infrastructure.Services;

// "Investigate" button on /shop-categorize - for a cluster the user can't
// place confidently from the label alone (e.g. "AMZN Mktp DE" could be shop
// supplies or personal shopping), ask Claude to reason about the merchant
// name and any raw bank reference text and suggest an answer. Never applied
// automatically - purely informational, the user still makes the final call.
public sealed class InvestigationService : IInvestigationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Tool InvestigateTool = new()
    {
        Name = "investigate_transaction",
        Description = "Report findings about an ambiguous Danish bank transaction pattern",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["reasoning"] = JsonSerializer.SerializeToElement(new
                {
                    type = "string",
                    description = "1-2 short sentences in Danish explaining what this merchant/pattern most likely is, based on the name and any reference text - be concrete (e.g. name the actual company/service if recognizable)."
                }),
                ["suggestedScope"] = JsonSerializer.SerializeToElement(new
                {
                    type = "string",
                    @enum = new[] { "Business", "Private", "Unsure" },
                    description = "Best guess: Business (shop expense/revenue), Private (personal spending), or Unsure if genuinely ambiguous."
                }),
                ["confidence"] = JsonSerializer.SerializeToElement(new
                {
                    type = "string",
                    @enum = new[] { "High", "Medium", "Low" }
                })
            },
            Required = ["reasoning", "suggestedScope", "confidence"]
        }
    };

    private const string SystemPrompt = """
        You help a Danish small-business owner (a bookshop) figure out what an
        unclear recurring bank transaction actually is, on an account that
        mixes business and private spending. You get a merchant/pattern label,
        optional raw bank reference text, how many times it occurs, and the
        total amount (negative = money out, positive = money in).
        Reason from the merchant name and any reference text - if you
        recognize the company/service, say what it does. Be honest about
        uncertainty; don't force a confident answer when the evidence is thin.
        Always call the investigate_transaction tool with your answer, in Danish.
        """;

    private static readonly Tool SuggestMergesTool = new()
    {
        Name = "suggest_category_merges",
        Description = "Suggest groups of existing category names that represent the same broader kind of spending and could be merged into one",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["suggestions"] = JsonSerializer.SerializeToElement(new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["sourceNames"] = new { type = "array", items = new { type = "string" }, description = "2 or more of the given existing category names that belong together" },
                            ["suggestedTargetName"] = new { type = "string", description = "A short Danish name for the merged category" },
                            ["reasoning"] = new { type = "string", description = "One short sentence in Danish explaining why these belong together" }
                        },
                        required = new[] { "sourceNames", "suggestedTargetName", "reasoning" }
                    }
                })
            },
            Required = ["suggestions"]
        }
    };

    private const string SuggestMergesSystemPrompt = """
        You get a list of spending/income category names for a Danish
        person/business (mix of shop and private categories), each already
        assigned, possibly too granular - e.g. separate categories per gas
        station brand instead of one "Benzin", or one category per named
        person for family MobilePay transfers instead of one "Familie".
        Suggest groups of 2 or more categories from the given list that are
        clearly the same broader kind of thing and would read better merged
        into one broad category. The target level of granularity the user
        wants is broad top-level categories like "Dagligvarer", "Benzin",
        "Rejser", "Familie", "Kunder" - not still-narrow groupings.
        Only suggest a group when you're genuinely confident they belong
        together - do not force groupings, and never invent a category name
        that wasn't given to you as a source. Skip singletons - only propose
        actual groups. Always call the suggest_category_merges tool, even if
        you can only find a couple of solid groups (an empty suggestions list
        is fine if nothing clearly groups).
        """;

    private readonly AnthropicClient _client;

    public InvestigationService(IConfiguration configuration)
    {
        var apiKey = configuration["Claude:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Missing Claude:ApiKey configuration");

        _client = new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<InvestigationResult> InvestigateAsync(
        string label,
        string sampleRawDetails,
        int count,
        decimal totalAmount,
        CancellationToken cancellationToken = default)
    {
        var context = $"Merchant/pattern: \"{label}\"" +
            (string.IsNullOrWhiteSpace(sampleRawDetails) ? "" : $"\nRaw bank reference text: \"{sampleRawDetails}\"") +
            $"\nOccurs {count} time(s), total amount {totalAmount:F2} DKK.";

        var request = new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 300,
            System = SystemPrompt,
            Tools = [InvestigateTool],
            ToolChoice = new ToolChoiceTool { Name = "investigate_transaction" },
            Messages = [new() { Role = Role.User, Content = context }]
        };

        var response = await _client.Messages.Create(request, cancellationToken);

        ToolUseBlock? toolUse = null;
        foreach (var block in response.Content)
        {
            if (block.TryPickToolUse(out var tu))
            {
                toolUse = tu;
                break;
            }
        }

        if (toolUse is null)
            throw new InvalidOperationException("Claude did not call the investigate_transaction tool.");

        var json = JsonSerializer.Serialize(toolUse.Input);
        var raw = JsonSerializer.Deserialize<InvestigationRaw>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize investigation result.");

        return new InvestigationResult(raw.Reasoning, raw.SuggestedScope, raw.Confidence);
    }

    public async Task<List<MergeSuggestion>> SuggestMergesAsync(
        IReadOnlyList<(string Name, int Count, decimal Sum)> categories,
        CancellationToken cancellationToken = default)
    {
        var list = string.Join("\n", categories.Select(c => $"- \"{c.Name}\" ({c.Count} stk., {c.Sum:F2} DKK)"));

        var request = new MessageCreateParams
        {
            Model = Model.ClaudeHaiku4_5,
            MaxTokens = 1024,
            System = SuggestMergesSystemPrompt,
            Tools = [SuggestMergesTool],
            ToolChoice = new ToolChoiceTool { Name = "suggest_category_merges" },
            Messages = [new() { Role = Role.User, Content = $"Existing categories:\n{list}" }]
        };

        var response = await _client.Messages.Create(request, cancellationToken);

        ToolUseBlock? toolUse = null;
        foreach (var block in response.Content)
        {
            if (block.TryPickToolUse(out var tu))
            {
                toolUse = tu;
                break;
            }
        }

        if (toolUse is null)
            throw new InvalidOperationException("Claude did not call the suggest_category_merges tool.");

        var json = JsonSerializer.Serialize(toolUse.Input);
        var raw = JsonSerializer.Deserialize<SuggestMergesRaw>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize merge suggestions.");

        var validNames = categories.Select(c => c.Name).ToHashSet();

        return raw.Suggestions
            // Defensive: only keep suggestions that actually reference real
            // category names and have 2+ of them - a hallucinated or
            // single-item "group" isn't a merge.
            .Where(s => s.SourceNames.Count >= 2 && s.SourceNames.All(validNames.Contains))
            .Select(s => new MergeSuggestion(s.SourceNames, s.SuggestedTargetName, s.Reasoning))
            .ToList();
    }

    private sealed class InvestigationRaw
    {
        public string Reasoning { get; set; } = string.Empty;
        public string SuggestedScope { get; set; } = "Unsure";
        public string Confidence { get; set; } = "Low";
    }

    private sealed class SuggestMergesRaw
    {
        public List<SuggestionRaw> Suggestions { get; set; } = new();
    }

    private sealed class SuggestionRaw
    {
        public List<string> SourceNames { get; set; } = new();
        public string SuggestedTargetName { get; set; } = string.Empty;
        public string Reasoning { get; set; } = string.Empty;
    }
}
