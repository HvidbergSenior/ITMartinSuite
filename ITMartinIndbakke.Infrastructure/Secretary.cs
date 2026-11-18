using System.Net.Http.Json;
using System.Text.Json;
using ITMartinIndbakke.Application;
using ITMartinIndbakke.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinIndbakke.Infrastructure;

/// <summary>
/// /data/datoer.json (found dates + the ids of mails already read for dates) and /data/venter.json (watched conversations).
/// Only date, time, keyword, sender, subject and ids - never mail text.
/// </summary>
public sealed class JsonSecretaryStore(string dataDir) : ISecretaryStore
{
    private sealed record DatesFile(List<MailDate> Dates, HashSet<string> Checked);

    private readonly object _gate = new();
    private DatesFile? _dates;
    private Dictionary<ulong, Watched>? _watched;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    private string DatesPath => Path.Combine(dataDir, "datoer.json");
    private string WatchedPath => Path.Combine(dataDir, "venter.json");

    public IReadOnlyList<MailDate> Dates() { lock (_gate) return LoadDates().Dates.ToList(); }

    public void SaveDate(MailDate date)
    {
        lock (_gate)
        {
            var f = LoadDates();
            f.Dates.RemoveAll(d => d.Id == date.Id);
            f.Dates.Add(date);
            // Old dates fall off after 60 days - the list never grows forever.
            f.Dates.RemoveAll(d => d.Date < DateOnly.FromDateTime(DateTime.Today).AddDays(-60));
            Write(DatesPath, f);
        }
    }

    public bool WasChecked(string messageId) { lock (_gate) return LoadDates().Checked.Contains(messageId); }

    public void MarkChecked(IEnumerable<string> messageIds)
    {
        lock (_gate)
        {
            var f = LoadDates();
            foreach (var id in messageIds) f.Checked.Add(id);
            if (f.Checked.Count > 20_000) f.Checked.Clear();   // a safety valve; at worst a few mails are read again
            Write(DatesPath, f);
        }
    }

    public IReadOnlyList<Watched> Watched() { lock (_gate) return LoadWatched().Values.ToList(); }

    public void SaveWatched(Watched watched)
    {
        lock (_gate)
        {
            var w = LoadWatched();
            w[watched.ThreadId] = watched;
            Write(WatchedPath, w.Values.ToList());
        }
    }

    private DatesFile LoadDates() => _dates ??= File.Exists(DatesPath)
        ? JsonSerializer.Deserialize<DatesFile>(File.ReadAllText(DatesPath), Json) ?? new([], [])
        : new([], []);

    private Dictionary<ulong, Watched> LoadWatched() => _watched ??= File.Exists(WatchedPath)
        ? (JsonSerializer.Deserialize<List<Watched>>(File.ReadAllText(WatchedPath), Json) ?? []).ToDictionary(w => w.ThreadId)
        : [];

    private static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Json));
        File.Move(tmp, path, overwrite: true);
    }
}

/// <summary>Husk's own API (POST /api/bolde, upsert by key, X-Husk-Key). Inside the photoserver compose: http://husk-web:8080.</summary>
public sealed class HuskClient(HttpClient http, string apiKey) : IHusk
{
    public bool IsConfigured => apiKey.Length >= 8 && http.BaseAddress is not null;

    public async Task<string?> SaveAsync(HuskBall ball, CancellationToken ct)
    {
        if (!IsConfigured) return "Husk er ikke sat op.";
        var body = new[]
        {
            new
            {
                key = ball.Key, title = ball.Title, area = ball.Area, status = ball.Status, nextStep = ball.NextStep,
                waitingOn = ball.WaitingOn, note = ball.Note, deadline = ball.Deadline?.ToString("yyyy-MM-dd"),
            },
        };
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/bolde") { Content = JsonContent.Create(body) };
            req.Headers.Add("X-Husk-Key", apiKey);
            using var res = await http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) return $"Husk svarede {(int)res.StatusCode}.";
            var result = await res.Content.ReadFromJsonAsync<JsonElement>(ct);
            return result.TryGetProperty("errors", out var e) && e.GetArrayLength() > 0 ? "Husk: " + e[0].GetString() : null;
        }
        catch (Exception e) when (!ct.IsCancellationRequested) { return "Husk kunne ikke nås: " + e.Message; }
    }
}

public static class IndbakkeSecretaryInfrastructure
{
    /// <summary>Indbakke__HuskUrl (default http://husk-web:8080) + Husk__ApiKey (the same key Husk itself uses, in magic.env).</summary>
    public static IServiceCollection AddIndbakkeSecretary(this IServiceCollection services, string? huskUrl, string? huskKey, string dataDir)
    {
        services.AddSingleton<ISecretaryStore>(new JsonSecretaryStore(dataDir));
        services.AddSingleton<IMailText>(sp => (IMailText)sp.GetRequiredService<IMailbox>());
        services.AddHttpClient("husk", c =>
        {
            if (Uri.TryCreate(string.IsNullOrWhiteSpace(huskUrl) ? "http://husk-web:8080" : huskUrl, UriKind.Absolute, out var u)) c.BaseAddress = u;
            c.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddSingleton<IHusk>(sp => new HuskClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient("husk"), huskKey ?? ""));
        return services;
    }
}
