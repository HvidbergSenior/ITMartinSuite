using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ITMartinKunder.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ITMartinKunder.Client;

// Talks to Kunder. When Kunder does not answer, the message waits in the app's own outbox folder and is sent again every
// minute - the person still gets "Tak, du er skrevet op". Only a real "no" from Kunder (bad e-mail) reaches the person.
public sealed class KunderClient(HttpClient http, KunderOutbox outbox, ILogger<KunderClient> log)
{
    /// <returns>Error for the person (Danish), or null when they are on the list (or will be, from the outbox).</returns>
    public async Task<string?> SignUpAsync(SignUpRequest req, CancellationToken ct = default)
    {
        req = req with { Email = KundeWords.Normalize(req.Email), AtUtc = req.AtUtc ?? DateTime.UtcNow };
        if (!KundeWords.IsRealEmail(req.Email)) return "Skriv en rigtig e-mail, fx navn@gmail.com.";
        return await SendOrQueueAsync(KunderApi.SignUp, req, ct);
    }

    /// <returns>Error for the person; else their kundenummer, or "" when Kunder did not answer (the sign-up waits in the
    /// outbox and Martin sends the number by mail later).</returns>
    public async Task<(string? Error, string Code)> SignUpWithCodeAsync(SignUpRequest req, CancellationToken ct = default)
    {
        req = req with { Email = KundeWords.Normalize(req.Email), AtUtc = req.AtUtc ?? DateTime.UtcNow };
        if (!KundeWords.IsRealEmail(req.Email)) return ("Skriv en rigtig e-mail, fx navn@gmail.com.", "");
        try
        {
            using var res = await http.PostAsJsonAsync(KunderApi.SignUpWithCode, req, Json, ct);
            if (res.IsSuccessStatusCode) return (null, (await res.Content.ReadFromJsonAsync<SignUpResponse>(Json, ct))?.Code ?? "");
            if (res.StatusCode == HttpStatusCode.BadRequest)
                return ((await res.Content.ReadFromJsonAsync<ErrorResponse>(Json, ct))?.Error ?? "Det kunne ikke gemmes.", "");
            throw new HttpRequestException($"Kunder answered {(int)res.StatusCode}");
        }
        catch (Exception ex) when (Unreachable(ex, ct))
        {
            log.LogWarning("Kunder did not answer ({Message}) - sign-up kept in the outbox", ex.Message);
            outbox.Add(KunderApi.SignUp, req);
            return (null, "");
        }
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime At, CodeCheckResponse Answer)> _codes = new();

    /// <summary>May this kundenummer use the extras in this app? A yes is remembered for 10 minutes. When Kunder does not
    /// answer, the person is told to try again in a moment (nothing is let in unchecked).</summary>
    public async Task<CodeCheckResponse> CheckCodeAsync(string? code, string app, CancellationToken ct = default)
    {
        var k = KundeWords.NormalizeCode(code);
        if (k.Length != 8) return new(false, Error: "Dit kundenummer har 8 cifre – du finder det i mailen fra Martin.");
        var key = app.ToLowerInvariant() + "|" + k;
        if (_codes.TryGetValue(key, out var hit) && DateTime.UtcNow - hit.At < TimeSpan.FromMinutes(10)) return hit.Answer;
        try
        {
            using var res = await http.PostAsJsonAsync(KunderApi.CheckCode, new CodeCheckRequest(k, app), Json, ct);
            if (!res.IsSuccessStatusCode) throw new HttpRequestException($"Kunder answered {(int)res.StatusCode}");
            var answer = await res.Content.ReadFromJsonAsync<CodeCheckResponse>(Json, ct) ?? new(false, Error: "Prøv igen om lidt.");
            if (answer.Ok) _codes[key] = (DateTime.UtcNow, answer); else _codes.TryRemove(key, out _);
            return answer;
        }
        catch (Exception ex) when (Unreachable(ex, ct))
        {
            log.LogWarning("Kunder did not answer the code check ({Message})", ex.Message);
            return new(false, Error: "Vi kan ikke tjekke dit kundenummer lige nu – prøv igen om et par minutter.");
        }
    }

    public Task BoughtAsync(PurchaseRequest req, CancellationToken ct = default) =>
        SendOrQueueAsync(KunderApi.Bought, req with { Email = KundeWords.Normalize(req.Email) }, ct);

    public Task ImportAsync(ImportRequest req, CancellationToken ct = default) =>
        SendOrQueueAsync(KunderApi.Import, req with { Email = KundeWords.Normalize(req.Email) }, ct);

    /// <summary>Always "done" for the person: unknown e-mails get the same answer, and a queued one is applied later.</summary>
    public Task UnsubscribeAsync(string email, CancellationToken ct = default) =>
        SendOrQueueAsync(KunderApi.Unsubscribe, new UnsubscribeRequest(KundeWords.Normalize(email)), ct);

    /// <returns>Found = the link was right (Email set when Kunder answered now; empty when it waits in the outbox).</returns>
    public async Task<UnsubscribeResponse> UnsubscribeByLinkAsync(string token, CancellationToken ct = default)
    {
        if (token.Length < 16) return new(false);
        try
        {
            using var res = await http.PostAsJsonAsync(KunderApi.UnsubscribeLink, new UnsubscribeLinkRequest(token), ct);
            if (res.IsSuccessStatusCode) return await res.Content.ReadFromJsonAsync<UnsubscribeResponse>(ct) ?? new(false);
            if ((int)res.StatusCode >= 500) throw new HttpRequestException($"Kunder {(int)res.StatusCode}");
            return new(false);
        }
        catch (Exception ex) when (Unreachable(ex, ct))
        {
            outbox.Add(KunderApi.UnsubscribeLink, new UnsubscribeLinkRequest(token));
            return new(true);
        }
    }

    private static IReadOnlyList<FreeAppInfo> _freeApps = [];
    private static DateTime _freeAppsAt = DateTime.MinValue;

    /// <summary>The free apps Martin has switched on in Kunder. Asked at most every 2 minutes; while Kunder does not
    /// answer, the last list it gave is used (empty until the first answer).</summary>
    public async Task<IReadOnlyList<FreeAppInfo>> FreeAppsAsync(CancellationToken ct = default)
    {
        if (DateTime.UtcNow - _freeAppsAt < TimeSpan.FromMinutes(2)) return _freeApps;
        try
        {
            _freeApps = await http.GetFromJsonAsync<List<FreeAppInfo>>(KunderApi.FreeApps, ct) ?? [];
            _freeAppsAt = DateTime.UtcNow;
        }
        catch (Exception ex) when (Unreachable(ex, ct) || ex is JsonException)
        {
            _freeAppsAt = DateTime.UtcNow - TimeSpan.FromMinutes(1.5);   // try again in half a minute
            log.LogWarning("Kunder did not give the free apps ({Message}) - using the last list", ex.Message);
        }
        return _freeApps;
    }

    /// <summary>The ticked helpers (Vareinfo's session page). Empty when Kunder does not answer.</summary>
    public async Task<List<HelperInfo>> HelpersAsync(CancellationToken ct = default)
    {
        try { return await http.GetFromJsonAsync<List<HelperInfo>>(KunderApi.Helpers, ct) ?? []; }
        catch (Exception ex) when (Unreachable(ex, ct) || ex is JsonException)
        {
            log.LogWarning("Kunder did not give the helpers ({Message})", ex.Message);
            return [];
        }
    }

    private async Task<string?> SendOrQueueAsync<T>(string path, T body, CancellationToken ct)
    {
        try
        {
            var (ok, error) = await PostAsync(path, JsonSerializer.Serialize(body, Json), ct);
            if (ok) return null;
            if (error is not null) return error;
        }
        catch (Exception ex) when (Unreachable(ex, ct)) { log.LogWarning("Kunder did not answer ({Message}) - kept in the outbox", ex.Message); }
        outbox.Add(path, body);
        return null;
    }

    /// <returns>ok, or the person's error when Kunder said no (400). Throws when Kunder is down or broken (5xx, 401).</returns>
    internal async Task<(bool Ok, string? Error)> PostAsync(string path, string json, CancellationToken ct)
    {
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var res = await http.PostAsync(path, content, ct);
        if (res.IsSuccessStatusCode) return (true, null);
        if (res.StatusCode == HttpStatusCode.BadRequest)
            return (false, (await res.Content.ReadFromJsonAsync<ErrorResponse>(ct))?.Error ?? "Det kunne ikke gemmes.");
        throw new HttpRequestException($"Kunder answered {(int)res.StatusCode}");
    }

    private static bool Unreachable(Exception ex, CancellationToken ct) =>
        ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested);

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
}

/// <summary>Messages for Kunder that could not be sent yet: one JSON file each in &lt;dataDir&gt;/kunder-outbox.</summary>
public sealed class KunderOutbox(string folder)
{
    public string Folder { get; } = folder;

    internal sealed record Item(string Path, string Body);

    public void Add<T>(string path, T body)
    {
        Directory.CreateDirectory(Folder);
        var name = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}";
        var tmp = System.IO.Path.Combine(Folder, name + ".tmp");
        File.WriteAllText(tmp, JsonSerializer.Serialize(new Item(path, JsonSerializer.Serialize(body, KunderClient.Json)), KunderClient.Json));
        File.Move(tmp, System.IO.Path.Combine(Folder, name + ".json"));   // never half a file
    }

    public int Count => Directory.Exists(Folder) ? Directory.GetFiles(Folder, "*.json").Length : 0;

    /// <summary>Sends what waits, oldest first. Stops at the first one Kunder does not take (it is still down).</summary>
    internal async Task<int> FlushAsync(KunderClient client, ILogger log, CancellationToken ct)
    {
        if (!Directory.Exists(Folder)) return 0;
        var sent = 0;
        foreach (var file in Directory.GetFiles(Folder, "*.json").Order())
        {
            var item = JsonSerializer.Deserialize<Item>(await File.ReadAllTextAsync(file, ct), KunderClient.Json);
            if (item is null) { File.Delete(file); continue; }
            var (ok, error) = await client.PostAsync(item.Path, item.Body, ct);   // throws while Kunder is down
            if (!ok)
            {
                // Kunder said no to this one (bad data) - kept aside for Martin to look at, the rest can go.
                log.LogWarning("Kunder refused {File}: {Error}", System.IO.Path.GetFileName(file), error);
                Directory.CreateDirectory(System.IO.Path.Combine(Folder, "afvist"));
                File.Move(file, System.IO.Path.Combine(Folder, "afvist", System.IO.Path.GetFileName(file)));
                continue;
            }
            File.Delete(file);
            sent++;
        }
        return sent;
    }
}

internal sealed class KunderOutboxJob(IServiceProvider services, KunderOutbox outbox, ILogger<KunderOutboxJob> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            if (outbox.Count == 0) continue;
            try
            {
                using var scope = services.CreateScope();
                var sent = await outbox.FlushAsync(scope.ServiceProvider.GetRequiredService<KunderClient>(), log, ct);
                if (sent > 0) log.LogInformation("Kunder outbox: sent {Sent}, {Left} waiting", sent, outbox.Count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                log.LogInformation("Kunder still not answering - {Left} waiting in the outbox", outbox.Count);
            }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}

public static class KunderClientExtensions
{
    /// <summary>Kunder__Url (default http://10.0.0.200:9155, the photoserver) + Kunder__ApiKey, same names in every app.
    /// The outbox lives in &lt;dataDir&gt;/kunder-outbox.</summary>
    public static IServiceCollection AddKunderClient(this IServiceCollection services, IConfiguration cfg, string dataDir) =>
        services.AddKunderClient(cfg["Kunder:Url"], cfg["Kunder:ApiKey"], dataDir);

    public static IServiceCollection AddKunderClient(this IServiceCollection services, string? url, string? apiKey, string dataDir)
    {
        services.AddSingleton(new KunderOutbox(Path.Combine(dataDir, "kunder-outbox")));
        services.AddHttpClient<KunderClient>(c =>
        {
            c.BaseAddress = new Uri(string.IsNullOrWhiteSpace(url) ? "http://10.0.0.200:9155" : url);
            c.Timeout = TimeSpan.FromSeconds(8);
            c.DefaultRequestHeaders.Add(KunderApi.KeyHeader, apiKey ?? "");
        });
        services.AddHostedService<KunderOutboxJob>();
        return services;
    }
}
