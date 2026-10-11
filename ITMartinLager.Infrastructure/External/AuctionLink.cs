using System.Net.Http.Json;

namespace ITMartinLager.Infrastructure.External;


// Talks to Live Auktion (photoserver :9140) server to server: list open auctions, send an item, fetch results.
// Lager__AuctionUrl (internal address) + Lager__AuctionKey (= Auction__LagerKey) in magic.env.
public sealed class AuctionLink(HttpClient http, IConfiguration cfg) : IAuctionGateway
{
    private string Base => (cfg["Lager:AuctionUrl"] ?? "http://10.0.0.200:9140").TrimEnd('/');
    public string PublicBase => (cfg["Lager:AuctionPublicUrl"] ?? "https://auktion.itmartin.dk").TrimEnd('/');
    public bool Enabled => (cfg["Lager:AuctionKey"] ?? "").Length >= 16;

    private HttpRequestMessage Req(HttpMethod m, string path, object? body = null)
    {
        var r = new HttpRequestMessage(m, Base + path);
        r.Headers.Add("X-Lager-Key", cfg["Lager:AuctionKey"]);
        if (body is not null) r.Content = JsonContent.Create(body);
        return r;
    }

    public async Task<List<AuctionSessionInfo>> SessionsAsync(CancellationToken ct)
    {
        using var res = await http.SendAsync(Req(HttpMethod.Get, "/api/lager/sessions"), ct);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<List<AuctionSessionInfo>>(ct) ?? [];
    }

    public async Task<Guid> SendAsync(string code, string name, string description, decimal startPrice, int lot, byte[]? jpeg, CancellationToken ct)
    {
        using var res = await http.SendAsync(Req(HttpMethod.Post, "/api/lager/items", new
        {
            Code = code, Name = name, Description = description, StartingPrice = startPrice, LotQuantity = lot,
            PhotoJpegBase64 = jpeg is null ? null : Convert.ToBase64String(jpeg),
        }), ct);
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"Live Auktion svarede {(int)res.StatusCode}: {await res.Content.ReadAsStringAsync(ct)}");
        var body = await res.Content.ReadFromJsonAsync<SentOut>(ct);
        return body!.Id;
    }

    public async Task<List<AuctionStatusInfo>> StatusAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        using var res = await http.SendAsync(Req(HttpMethod.Post, "/api/lager/status", ids.ToList()), ct);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<List<AuctionStatusInfo>>(ct) ?? [];
    }

        private sealed record SentOut(Guid Id, string Code);
}
