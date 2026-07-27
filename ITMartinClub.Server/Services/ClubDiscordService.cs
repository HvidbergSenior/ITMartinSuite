using System.Text.Json;

namespace ITMartinClub.Server.Services;

// Posts to a Discord incoming webhook - the cheapest possible integration
// (no bot hosting, no OAuth). Silently unavailable until a webhook URL is
// configured, same IsAvailable-gated pattern as ClubAiService/MatchOcrService.
public sealed class ClubDiscordService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly string? _webhookUrl;

    public ClubDiscordService(IHttpClientFactory httpFactory, IConfiguration config)
    {
        _httpFactory = httpFactory;
        _webhookUrl = config["ClubSettings:DiscordWebhookUrl"];
    }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_webhookUrl);

    public async Task<bool> PostMessageAsync(string content)
    {
        if (!IsAvailable) return false;

        var http = _httpFactory.CreateClient();
        var body = JsonSerializer.Serialize(new { content });
        var resp = await http.PostAsync(_webhookUrl,
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        return resp.IsSuccessStatusCode;
    }
}
