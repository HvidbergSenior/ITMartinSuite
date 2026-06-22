using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using ITMartinMusic.Server.Data.Entities;

namespace ITMartinMusic.Server.Services;

public sealed class AiCoachService
{
    private readonly AnthropicClient? _client;

    public bool IsAvailable => _client is not null;

    public AiCoachService(IConfiguration config)
    {
        var key = config["Claude:ApiKey"];
        if (!string.IsNullOrWhiteSpace(key))
            _client = new AnthropicClient { ApiKey = key };
    }

    public async Task<string> CoachAsync(Song song)
    {
        if (_client is null)
            return "AI coaching requires a Claude API key (Claude:ApiKey in config).";

        var prompt = $"""
            I'm working on an original song I wrote myself. Please give me constructive, encouraging feedback.

            Title: {song.Title}
            Key: {song.Key}
            {(song.Tempo.HasValue ? $"Tempo: {song.Tempo} BPM" : "")}

            Chord progression:
            {song.ChordChart}

            Lyrics:
            {song.Lyrics}

            Please give me:
            1. Feedback on the lyrics — flow, imagery, emotional impact, and any lines that really work
            2. Chord progression notes — does it work well in {song.Key}? Any chord substitutions worth trying?
            3. Transposing suggestion — would a different key suit a typical singing voice better?
            4. One concrete rewrite suggestion for a lyric line that could be stronger

            Keep it practical and direct.
            """;

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeOpus4_8,
            MaxTokens = 1024,
            Messages = [new() { Role = Role.User, Content = prompt }]
        });

        return ExtractText(response.Content) ?? "No response.";
    }

    private static string? ExtractText(object content)
    {
        var json = JsonSerializer.Serialize(content);
        using var doc = JsonDocument.Parse(json);
        foreach (var block in doc.RootElement.EnumerateArray())
        {
            if (block.TryGetProperty("type", out var t) && t.GetString() == "text" &&
                block.TryGetProperty("text", out var text))
                return text.GetString();
        }
        return null;
    }

    public async Task<string> ImproveLyricsAsync(string lyrics, string instruction)
    {
        if (_client is null)
            return string.Empty;

        var prompt = $"""
            Here are song lyrics I wrote:

            {lyrics}

            My request: {instruction}

            Please provide only the revised lyrics, keeping my voice and style. No commentary.
            """;

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeOpus4_8,
            MaxTokens = 1024,
            Messages = [new() { Role = Role.User, Content = prompt }]
        });

        return ExtractText(response) ?? string.Empty;
    }
}
