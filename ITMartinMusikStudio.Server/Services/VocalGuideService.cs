using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ITMartinMusikStudio.Server.Services;

public sealed class VocalGuideService
{
    private readonly HttpClient _http;
    private readonly string? _apiKey;

    public bool IsAvailable => _apiKey is not null;

    public VocalGuideService(IConfiguration config, IHttpClientFactory factory)
    {
        _http    = factory.CreateClient("fal");
        _http.Timeout = TimeSpan.FromMinutes(5);
        _apiKey  = Environment.GetEnvironmentVariable("FalAi__ApiKey")
                   ?? config["FalAi:ApiKey"];
    }

    // Generate a "la la la" vocal guide track using fal-ai/stable-audio.
    // Returns the CDN URL of the generated .wav file.
    public async Task<string> GenerateAsync(
        string musicKey,
        int? tempo,
        string chordChart,
        int durationSeconds = 30,
        CancellationToken ct = default)
    {
        if (_apiKey is null) throw new InvalidOperationException("FalAi__ApiKey ikke konfigureret");

        var bpm    = tempo.HasValue ? $"{tempo} BPM, " : "";
        var chords = string.IsNullOrWhiteSpace(chordChart)
            ? ""
            : $" akkorder: {chordChart.Replace('\n', ' ').Trim()}.";

        var prompt =
            $"gentle vocal melody humming la la la, {bpm}key of {musicKey},{chords} " +
            "practice guide for singer, no drums, no bass, clean pure voice, " +
            "slow simple melodic phrase, repeating motif";

        var body = JsonSerializer.Serialize(new
        {
            prompt,
            seconds_total = durationSeconds,
            steps         = 100,
        });

        var req = new HttpRequestMessage(HttpMethod.Post, "https://fal.run/fal-ai/stable-audio")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Key", _apiKey);

        var resp = await _http.SendAsync(req, ct);
        var raw  = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"fal.ai {resp.StatusCode}: {raw}");

        var doc = JsonDocument.Parse(raw).RootElement;

        // stable-audio returns { "audio_file": { "url": "..." } }
        if (doc.TryGetProperty("audio_file", out var af) &&
            af.TryGetProperty("url", out var u))
            return u.GetString()!;

        // fallback: audio.url or url at root
        if (doc.TryGetProperty("audio", out var a) && a.TryGetProperty("url", out var au))
            return au.GetString()!;
        if (doc.TryGetProperty("url", out var ru))
            return ru.GetString()!;

        throw new InvalidOperationException($"Kunne ikke finde lyd-URL i svar: {raw[..Math.Min(300, raw.Length)]}");
    }
}
