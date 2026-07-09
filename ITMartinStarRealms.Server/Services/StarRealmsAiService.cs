using Anthropic;
using Anthropic.Models.Messages;

namespace ITMartinStarRealms.Server.Services;

public sealed class StarRealmsAiService
{
    private readonly AnthropicClient? _client;

    public StarRealmsAiService(IConfiguration config)
    {
        var key = config["Claude:ApiKey"];
        if (!string.IsNullOrWhiteSpace(key))
            _client = new AnthropicClient { ApiKey = key };
    }

    public bool IsAvailable => _client is not null;

    public async Task<string> GetShipHintAsync(string shipName, string faction)
    {
        if (_client is null) return "";

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeSonnet4_6,
            MaxTokens = 400,
            System = """
                You are an expert Star Realms (the deckbuilding card game) strategy coach.
                Given a ship or base name and its faction, give a short, punchy strategy tip:
                what it combos well with (same-faction ally abilities, scrap synergies, faction
                bonuses), and when it's strong to buy or play.
                If you are not fully certain of a card's exact printed text or ability, say so
                briefly and give general faction-synergy advice instead of inventing false abilities.
                Keep it to 3-5 short sentences or bullet points. No preamble, just the tip.
                """,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = $"Card: {shipName} ({faction} faction). Give me a combo/strategy tip."
                }
            ]
        });

        foreach (var block in response.Content)
            if (block.TryPickText(out var tb)) return tb.Text.Trim();
        return "";
    }

    public async Task<string> AnalyzeTradeRowAsync(string base64Image, string mediaType)
    {
        if (_client is null) return "";

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeOpus4_8,
            MaxTokens = 700,
            System = """
                You are an expert Star Realms strategy coach looking at a photo of the trade row
                (the row of ship/base cards currently available to buy) and the player's current
                situation if visible (trade/combat resources, authority).

                1. List each card you can identify in the trade row, with its cost if visible.
                2. Recommend which card(s) are the best buy right now, and briefly say why
                   (faction synergy potential, tempo, scrap ability, authority gain, etc).
                3. If you can't clearly read a card, say so rather than guessing its name.

                Keep the whole answer compact — a short list plus 2-4 sentences of recommendation.
                """,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = new List<ContentBlockParam>
                    {
                        new TextBlockParam { Text = "Here's the current trade row. What should I buy?" },
                        new ImageBlockParam
                        {
                            Source = new Base64ImageSource { Data = base64Image, MediaType = mediaType }
                        }
                    }
                }
            ]
        });

        foreach (var block in response.Content)
            if (block.TryPickText(out var tb)) return tb.Text.Trim();
        return "";
    }
}
