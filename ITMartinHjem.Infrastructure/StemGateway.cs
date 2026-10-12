using System.Net;
using System.Net.Http.Json;
using ITMartinHjem.Application;

namespace ITMartinHjem.Infrastructure;

// Stem's /api/polls over the NAS docker network (Hjem:StemApi, default http://poll-web:8080).
public sealed class StemGateway(HttpClient http) : IPollGateway, ICustomerGateway
{
    private sealed record UnsubscribeResult(bool Found);

    public async Task<string?> SignUpAsync(string email, string name, string source, string consentText, string interests, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync("/api/customers", new { email, name, source, consentText, interests }, ct);
        if (res.IsSuccessStatusCode) return null;
        if (res.StatusCode == HttpStatusCode.BadRequest) return (await res.Content.ReadFromJsonAsync<ErrorBody>(ct))?.Error ?? "Kunne ikke gemmes.";
        res.EnsureSuccessStatusCode();
        return null;
    }

    public async Task<bool> UnsubscribeAsync(string email, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync("/api/customers/unsubscribe", new { email }, ct);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<UnsubscribeResult>(ct))?.Found ?? false;
    }

    private sealed record ErrorBody(string? Error, PollView? Poll);

    public async Task<PollView?> GetAsync(int pollId, CancellationToken ct = default)
    {
        using var res = await http.GetAsync($"/api/polls/{pollId}", ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<PollView>(ct);
    }

    public async Task<(string? Error, PollView? Poll)> VoteAsync(int pollId, PollVote vote, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync($"/api/polls/{pollId}/vote", new
        {
            optionIds = vote.OptionIds, name = vote.Name, email = vote.Email, comment = vote.Comment, auctionNews = vote.AuctionNews,
        }, ct);
        if (res.IsSuccessStatusCode) return (null, await res.Content.ReadFromJsonAsync<PollView>(ct));
        if (res.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
        {
            var body = await res.Content.ReadFromJsonAsync<ErrorBody>(ct);
            return (body?.Error ?? "Stemmen kunne ikke gemmes.", body?.Poll);
        }
        res.EnsureSuccessStatusCode();
        return ("Stemmen kunne ikke gemmes.", null);
    }
}
