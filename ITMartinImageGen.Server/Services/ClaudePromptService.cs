using Anthropic;
using Anthropic.Models.Messages;

namespace ITMartinImageGen.Server.Services;

public sealed class ClaudePromptService
{
    private readonly AnthropicClient _client;

    private const string SystemPrompt = """
        You are an expert AI image prompt engineer specializing in product photography and commercial imagery.
        Your job is to convert user descriptions into detailed, optimized prompts for the Flux image generation model.

        Rules:
        - Write in English (Flux performs best with English prompts)
        - Be specific: describe lighting (e.g. "professional studio lighting, softbox"), background, composition, style
        - For product/commercial use: prefer clean white or neutral backgrounds unless asked otherwise
        - Include quality tags at the end: "sharp focus, high resolution, professional photography, commercial quality"
        - Return ONLY the prompt — no explanations, no quotes
        """;

    private const string FeedbackSystemPrompt = """
        You are an expert AI image prompt engineer. The user has generated an image and wants changes.
        Look at the image and the user's feedback, then write an improved prompt that fixes the issues.

        Rules:
        - Keep what was good from the previous prompt
        - Address exactly what the user asked to change
        - Write in English
        - Return ONLY the new prompt — no explanations, no quotes
        """;

    public ClaudePromptService(IConfiguration config)
    {
        var apiKey = config["Claude:ApiKey"] ?? throw new InvalidOperationException("Claude:ApiKey not configured");
        _client = new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<string> RefinePromptAsync(string userDescription, CancellationToken ct = default)
    {
        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model     = Model.ClaudeSonnet4_6,
            MaxTokens = 512,
            System    = SystemPrompt,
            Messages  =
            [
                new() { Role = Role.User, Content = userDescription }
            ]
        }, cancellationToken: ct);

        return ExtractText(response);
    }

    public async Task<string> AnalyzeAndRefineAsync(
        string imageUrl,
        string userFeedback,
        string currentPrompt,
        CancellationToken ct = default)
    {
        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model     = Model.ClaudeSonnet4_6,
            MaxTokens = 512,
            System    = FeedbackSystemPrompt,
            Messages  =
            [
                new()
                {
                    Role    = Role.User,
                    Content = new List<ContentBlockParam>
                    {
                        new ImageBlockParam
                        {
                            Source = new UrlImageSource { Url = imageUrl }
                        },
                        new TextBlockParam
                        {
                            Text = $"Current prompt: {currentPrompt}\n\nUser feedback: {userFeedback}\n\nWrite the improved prompt:"
                        }
                    }
                }
            ]
        }, cancellationToken: ct);

        return ExtractText(response);
    }

    public async Task<string> TranslateEditInstructionAsync(string instruction, CancellationToken ct = default)
    {
        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model     = Model.ClaudeSonnet4_6,
            MaxTokens = 120,
            System    = "Translate the user's image editing instruction to clear, concise English. Keep it short and specific. Return ONLY the translated instruction — no explanations, no quotes.",
            Messages  = [ new() { Role = Role.User, Content = instruction } ]
        }, cancellationToken: ct);
        return ExtractText(response);
    }

    private static string ExtractText(Message response)
    {
        var text = new System.Text.StringBuilder();
        foreach (var block in response.Content)
        {
            if (block.TryPickText(out var t))
                text.Append(t.Text);
        }
        return text.ToString().Trim();
    }
}
