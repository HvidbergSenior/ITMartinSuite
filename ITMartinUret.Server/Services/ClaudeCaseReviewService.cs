using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace ITMartinUret.Server.Services;

public sealed class ClaudeCaseReviewService : ICaseReviewService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly Tool RiskCheckTool = new()
    {
        Name = "report_risk",
        Description = "Report the legal risk assessment of a user-submitted consumer complaint",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["level"] = JsonDocument.Parse("""
                    { "type": "string", "enum": ["None", "Low", "Medium", "High"] }
                    """).RootElement,
                ["flags"] = JsonDocument.Parse("""
                    {
                        "type": "array",
                        "items": { "type": "string" },
                        "description": "Short tags for each risky element found, e.g. 'unverified criminal accusation', 'insult', 'threat', 'names a private individual'"
                    }
                    """).RootElement,
                ["explanation"] = JsonDocument.Parse("""
                    { "type": "string", "description": "One or two sentences explaining the assessment, in Danish" }
                    """).RootElement,
            },
            Required = ["level", "flags", "explanation"]
        }
    };

    private readonly AnthropicClient _client;
    private readonly ILogger<ClaudeCaseReviewService> _logger;

    public ClaudeCaseReviewService(IConfiguration configuration, ILogger<ClaudeCaseReviewService> logger)
    {
        _logger = logger;
        var apiKey = configuration["Claude:ApiKey"]
            ?? throw new InvalidOperationException("Missing Claude API key");
        _client = new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<RiskCheckResult> CheckRiskAsync(string company, string body, CancellationToken ct = default)
    {
        var request = new MessageCreateParams
        {
            Model = Model.ClaudeOpus4_8,
            MaxTokens = 512,
            System = """
                You review user-submitted consumer complaint posts for a Danish website before publication.
                The site's policy: posts must state facts only (dates, what was said/written, what happened) —
                no accusations of crimes, no insults, no unverified claims of illegal conduct, no personal data
                about named private individuals (only the company as an organization may be named).

                Assess legal risk under Danish defamation law (injurier/bagvaskelse) and GDPR.
                Flag: accusations of crimes or fraud without evidence, insulting/inflammatory language,
                threats, and personal data about identifiable private individuals (e.g. a named employee).
                Do NOT flag merely naming the company itself, or a plain factual account of dates and events.
                """,
            Tools = [RiskCheckTool],
            ToolChoice = new ToolChoiceTool { Name = "report_risk" },
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = $"Company: {company}\n\nCase text:\n{body}"
                }
            ]
        };

        var response = await _client.Messages.Create(request, ct);

        ToolUseBlock? toolUse = null;
        foreach (var block in response.Content)
        {
            if (block.TryPickToolUse(out var tu)) { toolUse = tu; break; }
        }

        if (toolUse is null) return new RiskCheckResult(RiskLevel.None, [], "");

        var json = JsonSerializer.Serialize(toolUse.Input);
        _logger.LogDebug("Risk check response: {Json}", json);

        using var doc = JsonDocument.Parse(json);
        var level = Enum.TryParse<RiskLevel>(doc.RootElement.GetProperty("level").GetString(), out var l)
            ? l : RiskLevel.None;
        var flags = doc.RootElement.TryGetProperty("flags", out var arr)
            ? JsonSerializer.Deserialize<List<string>>(arr.GetRawText(), JsonOptions) ?? []
            : [];
        var explanation = doc.RootElement.TryGetProperty("explanation", out var exp) ? exp.GetString() ?? "" : "";

        return new RiskCheckResult(level, flags, explanation);
    }

    public async Task<string> RewriteAsFactualAsync(string body, CancellationToken ct = default)
    {
        var request = new MessageCreateParams
        {
            Model = Model.ClaudeSonnet4_5,
            MaxTokens = 1024,
            System = """
                Rewrite the user's text into a neutral, facts-only account in Danish: dates, what was said/written,
                what happened, what the user did. Remove insults, editorializing, and unverified accusations of
                crime or fraud — keep only what can be stated as fact. Keep the same events and level of detail.
                Return ONLY the rewritten text, no preamble, no quotes around it.
                """,
            Messages = [new() { Role = Role.User, Content = body }]
        };

        var response = await _client.Messages.Create(request, ct);

        foreach (var block in response.Content)
        {
            if (block.TryPickText(out var tb)) return tb.Text.Trim();
        }

        return body;
    }

    public async Task<string> SummarizeDocumentAsync(string company, byte[] fileBytes, string fileName, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var base64 = Convert.ToBase64String(fileBytes);

        ContentBlockParam documentBlock = ext switch
        {
            ".jpg" or ".jpeg" or ".png" => new ImageBlockParam
            {
                Source = new Base64ImageSource { Data = base64, MediaType = ext == ".png" ? "image/png" : "image/jpeg" }
            },
            ".pdf" => new DocumentBlockParam
            {
                Source = new Base64PdfSource { Data = base64 }
            },
            _ => new TextBlockParam { Text = System.Text.Encoding.UTF8.GetString(fileBytes) }
        };

        var request = new MessageCreateParams
        {
            Model = Model.ClaudeOpus4_8,
            MaxTokens = 768,
            System = $$"""
                You read an uploaded document (often an email or letter from {{company}}) attached to a Danish
                consumer complaint case. Produce a short factual resume in Danish: who it's from/to, the date,
                and the key statements relevant to the dispute. State only what the document actually says —
                do not add opinions, speculation, or legal conclusions. If the document is unreadable or blank,
                say so plainly instead of guessing.

                Privacy rule: if the sender/signer is a named individual employee (a person's name, not a
                shared department address like "kundeservice@{{company}}"), do NOT include that person's name
                in your resume — refer to them by role only (e.g. "en sagsbehandler hos {{company}}"). The
                company itself may always be named freely.
                """,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = new List<ContentBlockParam>
                    {
                        new TextBlockParam { Text = "Lav et kort, faktuelt resumé af dette dokument:" },
                        documentBlock,
                    }
                }
            ]
        };

        var response = await _client.Messages.Create(request, ct);

        foreach (var block in response.Content)
        {
            if (block.TryPickText(out var tb)) return tb.Text.Trim();
        }

        return "Kunne ikke læse dokumentet.";
    }

    public async Task<string> SuggestActionsAsync(string company, string body, CancellationToken ct = default)
    {
        var request = new MessageCreateParams
        {
            Model = Model.ClaudeOpus4_8,
            MaxTokens = 768,
            System = """
                You give general, practical guidance in Danish to someone describing a dispute with a company
                on a Danish consumer website. Based ONLY on what they described, suggest concrete next steps
                they could consider — e.g. contacting the police (only if the situation plausibly involves a
                crime, like fraud or identity theft), contacting Forbrugerklagenævnet or Forbrugerombudsmanden
                for a formal consumer complaint, writing back to the company with a specific, concrete request,
                or simply noting that no further action seems necessary if the matter already looks resolved.

                Rules:
                - Be concrete: name the actual authority/contact and what to ask for, not vague advice like
                  "consider your options."
                - If nothing serious is described, say so plainly — don't invent urgency.
                - This is general guidance, not formal legal advice. End with one short sentence recommending
                  a lawyer or the relevant authority directly for anything serious, financial, or unclear.
                - Write 3-5 short bullet points, in Danish. No preamble, no markdown headers — just the bullets.
                """,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = $"Company: {company}\n\nCase text:\n{body}"
                }
            ]
        };

        var response = await _client.Messages.Create(request, ct);

        foreach (var block in response.Content)
        {
            if (block.TryPickText(out var tb)) return tb.Text.Trim();
        }

        return "Kunne ikke generere forslag lige nu — prøv igen senere.";
    }
}
