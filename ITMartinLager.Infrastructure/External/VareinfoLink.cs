using System.Net.Http.Json;

namespace ITMartinLager.Infrastructure.External;

// Vareinfo's API for Lager (/api/lager/*), server to server on the LAN. The key is Lager__InfoKey here and
// Vareinfo__LagerKey there. Lager__VareinfoUrl = where to call (http://vareinfo-web:8080), Lager__VareinfoPublicUrl =
// the link a person clicks (https://vareinfo.bogshoppen.dk).
public sealed class VareinfoLink(HttpClient http, IConfiguration cfg) : IVareinfoGateway
{
    private string Base => (cfg["Lager:VareinfoUrl"] ?? "http://vareinfo-web:8080").TrimEnd('/');
    public string PublicBase => (cfg["Lager:VareinfoPublicUrl"] ?? "https://vareinfo.bogshoppen.dk").TrimEnd('/');

    private HttpRequestMessage Req(HttpMethod m, string path)
    {
        var req = new HttpRequestMessage(m, Base + path);
        req.Headers.Add("X-Info-Key", cfg["Lager:InfoKey"] ?? "");
        return req;
    }

    private sealed record Read(int PhotoId, List<Found> Found);

    public async Task<(int PhotoId, List<Found> Found)> ReadPileAsync(Stream photo, string hint, string label, CancellationToken ct)
    {
        using var req = Req(HttpMethod.Post, "/api/lager/laes");
        var form = new MultipartFormDataContent
        {
            { new StreamContent(photo), "photo", "photo.jpg" },
            { new StringContent(hint), "hint" },
            { new StringContent(label), "label" },
        };
        req.Content = form;
        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
            throw new LagerException((int)res.StatusCode == 503
                ? "Vareinfo kan ikke læse billeder lige nu (dagens grænse for AI-kald er måske nået)."
                : $"Vareinfo svarede ikke ({(int)res.StatusCode}). Prøv igen om lidt.");
        var r = (await res.Content.ReadFromJsonAsync<Read>(ct))!;
        return (r.PhotoId, r.Found);
    }

    public async Task<Found?> LookupBarcodeAsync(string code, CancellationToken ct)
    {
        using var req = Req(HttpMethod.Get, "/api/lager/stregkode/" + Uri.EscapeDataString(code));
        using var res = await http.SendAsync(req, ct);
        if (res.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        if (!res.IsSuccessStatusCode) throw new LagerException($"Vareinfo svarede ikke ({(int)res.StatusCode}). Prøv igen om lidt.");
        return await res.Content.ReadFromJsonAsync<Found>(ct);
    }
}
