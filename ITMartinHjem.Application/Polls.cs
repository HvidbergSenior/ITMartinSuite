namespace ITMartinHjem.Application;

// bogshoppen.dk shows Stem's polls itself and sends the votes to Stem (user 2026-10-12: "the questions should be here").
// Hjem only knows this contract; Stem's rules (open, one vote per e-mail, opt-in) run in Stem.
public sealed record PollOptionView(int Id, string Label, int People);
public sealed record PollView(int Id, string Title, string Body, bool AllowMultiple, bool RequireContact, bool Open,
    int People, IReadOnlyList<PollOptionView> Options, string OptInText, string Privacy, IReadOnlyList<string>? SignUpMeans = null);
public sealed record PollVote(IReadOnlyCollection<int> OptionIds, string Name, string Email, string Comment, bool AuctionNews);

public interface IPollGateway
{
    Task<PollView?> GetAsync(int pollId, CancellationToken ct = default);
    /// <returns>Error for the voter (Danish) or the poll with the new counts.</returns>
    Task<(string? Error, PollView? Poll)> VoteAsync(int pollId, PollVote vote, CancellationToken ct = default);
}

public sealed class SitePolls(IPollGateway stem)
{
    public const string Unreachable = "Afstemningen kan ikke nås lige nu – prøv igen om lidt.";

    public async Task<PollView?> GetAsync(int pollId, CancellationToken ct = default)
    {
        try { return await stem.GetAsync(pollId, ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return null; }
    }

    public async Task<(string? Error, PollView? Poll)> VoteAsync(int pollId, PollVote vote, CancellationToken ct = default)
    {
        if (vote.OptionIds.Count == 0) return ("Vælg mindst én mulighed.", null);
        try { return await stem.VoteAsync(pollId, vote, ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return (Unreachable, null); }
    }
}
