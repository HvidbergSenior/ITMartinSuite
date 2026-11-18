using System.Net.Http.Json;
using EasyPass.Application;
using EasyPass.Domain;
using Microsoft.Extensions.Configuration;

namespace EasyPass.Infrastructure;

/// <summary>Snak's room API (POST /api/rum with X-Snak-Key). EasyPass__SnakUrl + EasyPass__SnakKey (= Snak__ApiKey); unset = button hidden.</summary>
internal sealed class SnakRumKlient(HttpClient http, IConfiguration cfg) : ISnakRum
{
    private string Url => (cfg["EasyPass:SnakUrl"] ?? "").TrimEnd('/');
    private string Key => cfg["EasyPass:SnakKey"] ?? "";
    public bool Klar => Url.Length > 0 && Key.Length >= 16;

    public async Task<string?> OpretAsync(string titel, IReadOnlyList<string> navne, IReadOnlyList<TurTrin> trin, string slutDag, CancellationToken ct = default)
    {
        if (!Klar) return null;
        using var req = new HttpRequestMessage(HttpMethod.Post, Url + "/api/rum")
        {
            Content = JsonContent.Create(new
            {
                titel, kunde = "easypass", medlemmer = string.Join(", ", navne), slutDag,
                opgaver = trin.Select(t => new { titel = t.Titel, note = t.Note, dag = t.Dag, hvornaar = t.Hvornaar }),
            }),
        };
        req.Headers.Add("X-Snak-Key", Key);
        try
        {
            using var res = await http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) return null;
            var body = await res.Content.ReadFromJsonAsync<Svar>(cancellationToken: ct);
            return body?.Link;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException) { return null; }
    }

    private sealed record Svar(string? Link);
}
