using System.Net.Http.Json;
using ITMartinLager.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace ITMartinLager.Server.Services;

public sealed record AuctionSessionInfo(string Code, string Name, string Status, DateTime? AuctionDate, DateTime CreatedAt, DateTime ExpiresAt, int Items);
public sealed record AuctionStatusInfo(Guid Id, string Status, decimal? WinningBid, string? BuyerName, string? BuyerPhone);

// Talks to Live Auktion (photoserver :9140) server to server: list open auctions, send an item, fetch results.
// Lager__AuctionUrl (internal address) + Lager__AuctionKey (= Auction__LagerKey) in magic.env.
public sealed class AuctionLink(HttpClient http, IConfiguration cfg)
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

    public async Task<List<AuctionSessionInfo>> SessionsAsync(CancellationToken ct = default)
    {
        using var res = await http.SendAsync(Req(HttpMethod.Get, "/api/lager/sessions"), ct);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<List<AuctionSessionInfo>>(ct) ?? [];
    }

    public async Task<Guid> SendAsync(string code, string name, string description, decimal startPrice, int lot, byte[]? jpeg, CancellationToken ct = default)
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

    public async Task<List<AuctionStatusInfo>> StatusAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        using var res = await http.SendAsync(Req(HttpMethod.Post, "/api/lager/status", ids.ToList()), ct);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<List<AuctionStatusInfo>>(ct) ?? [];
    }

    // What an auction result means for the item in the register. Pending/Active = nothing yet.
    public static bool Apply(Item item, AuctionStatusInfo s, DateTime now)
    {
        switch (s.Status)
        {
            case "Sold":
                item.Status = Statuses.Solgt;
                item.SoldPrice = s.WinningBid;
                item.SoldTo = string.Join(", ", new[] { s.BuyerName, s.BuyerPhone }.Where(x => !string.IsNullOrWhiteSpace(x)));
                item.SoldAt = now;
                item.Note = Join(item.Note, $"Solgt på auktion {item.AuctionCode} {now:d/M yyyy}" + (s.WinningBid is { } b ? $" for {b:0} kr" : ""));
                break;
            case "Passed":
            case "Gone":
                item.Status = Statuses.Lager;
                item.Note = Join(item.Note, s.Status == "Passed"
                    ? $"Ikke solgt på auktion {item.AuctionCode} {now:d/M yyyy}"
                    : $"Fjernet fra auktion {item.AuctionCode} {now:d/M yyyy}");
                item.AuctionItemId = "";
                break;
            default:
                return false;
        }
        item.Touch();
        return true;
    }

    private static string Join(string a, string b) => string.IsNullOrWhiteSpace(a) ? b : a + " · " + b;

    private sealed record SentOut(Guid Id, string Code);
}

// Every 2 minutes: fetch results for items that are up for auction, so a sale is in the register long before
// Live Auktion cleans the auction away.
public sealed class AuctionSync(IServiceScopeFactory scopes, IDbContextFactory<LagerDb> dbf, ILogger<AuctionSync> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(2));
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var link = scope.ServiceProvider.GetRequiredService<AuctionLink>();
                if (!link.Enabled) continue;
                await using var db = await dbf.CreateDbContextAsync(ct);
                var open = await db.Items.Where(i => i.AuctionItemId != "" && i.Status == Statuses.PaaAuktion).ToListAsync(ct);
                if (open.Count == 0) continue;
                var ids = open.Select(i => Guid.TryParse(i.AuctionItemId, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList();
                var changed = 0;
                foreach (var s in await link.StatusAsync(ids, ct))
                {
                    var item = open.FirstOrDefault(i => i.AuctionItemId == s.Id.ToString());
                    if (item is not null && AuctionLink.Apply(item, s, DateTime.Now)) changed++;
                }
                if (changed > 0) { await db.SaveChangesAsync(ct); log.LogInformation("Auction sync: {Changed} items updated", changed); }
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { log.LogWarning(ex, "Auction sync failed"); }
        }
    }
}
