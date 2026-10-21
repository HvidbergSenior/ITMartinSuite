using ITMartinPoll.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinPoll.Application;

// The Kundekartotek is Kunder since 2026-10-21 (one list for all apps). The adapter keeps what Kunder does not take
// right now in an outbox and sends it later, so a voter is never turned away for that.
public interface ICustomerStore
{
    /// <summary>Marks the customer as unsubscribed (unknown e-mails get the same answer).</summary>
    Task UnsubscribeAsync(string email, CancellationToken ct = default);
    /// <returns>Found = the afmeld-link was right; Email when Kunder answered at once.</returns>
    Task<(bool Found, string Email)> UnsubscribeByLinkAsync(string token, CancellationToken ct = default);
    /// <summary>A customer who just said yes.</summary>
    Task UpsertAsync(CustomerOptIn optIn, CancellationToken ct = default);
    /// <summary>A customer from Stem's own old list (poll.db Customers, before 2026-10-21), with dates and afmeld-link.</summary>
    Task MoveAsync(Customer old, CancellationToken ct = default);
}

// Sign up from other sites (the old /api/customers - itmartin.dk used it before Kunder) and leave with one e-mail.
public sealed class CustomerList(ICustomerStore store, TimeProvider clock)
{
    public async Task<string?> SignUpAsync(string? email, string? name, string source, string consentText, string interests, CancellationToken ct = default)
    {
        var e = CustomerRules.Normalize(email);
        if (!VoteRules.IsRealEmail(e)) return "Skriv en rigtig e-mail, fx navn@gmail.com.";
        await store.UpsertAsync(new CustomerOptIn(e, (name ?? "").Trim(), source, consentText, interests, clock.GetUtcNow().UtcDateTime), ct);
        return null;
    }

    public Task UnsubscribeAsync(string? email, CancellationToken ct = default) => store.UnsubscribeAsync(CustomerRules.Normalize(email), ct);

    /// <summary>The link in mails already sent (stem.itmartin.dk/afmeld/{token}).</summary>
    public Task<(bool Found, string Email)> UnsubscribeByLinkAsync(string token, CancellationToken ct = default) =>
        token.Length < 16 ? Task.FromResult((false, "")) : store.UnsubscribeByLinkAsync(token, ct);

    /// <summary>Stem's old list into Kunder - once (Kunder leaves anyone it already has alone).</summary>
    public async Task<int> MoveOldListAsync(IAggregateStore<Customer> old, CancellationToken ct = default)
    {
        var all = await old.ListAsync(ct);
        foreach (var c in all) await store.MoveAsync(c, ct);
        return all.Count;
    }
}

public interface IVotingStore
{
    Task<Ballot?> GetAsync(int pollId, CancellationToken ct = default);
    /// <summary>Stores the vote rows (the opt-in goes to Kunder after, through ICustomerStore).</summary>
    Task SaveAsync(int pollId, AcceptedVote vote, CancellationToken ct = default);
}

// Voting in Stem: the poll page and other sites (bogshoppen.dk via /api/polls) go through here,
// so the rules (open/closed, one or more choices, contact, one vote per e-mail, opt-in) live in one place.
public sealed class Voting(IVotingStore store, ICustomerStore customers, TimeProvider clock)
{
    public sealed record OptionResult(int Id, string Label, int People);
    public sealed record PollView(int Id, string Title, string Body, bool AllowMultiple, bool RequireContact, bool Open,
        int People, IReadOnlyList<OptionResult> Options, string OptInText, string Privacy, IReadOnlyList<string> SignUpMeans);

    public async Task<PollView?> ViewAsync(int pollId, CancellationToken ct = default)
    {
        var p = await store.GetAsync(pollId, ct);
        return p is null ? null : View(p);
    }

    /// <returns>Error (Danish, for the voter) or the poll with the new counts.</returns>
    public async Task<(string? Error, PollView? Poll)> VoteAsync(int pollId, VoteRequest req, CancellationToken ct = default)
    {
        var p = await store.GetAsync(pollId, ct);
        if (p is null) return ("Afstemningen findes ikke.", null);
        var (error, vote) = VoteRules.Accept(p, req, clock.GetUtcNow().UtcDateTime);
        if (error is not null) return (error, View(p));
        await store.SaveAsync(pollId, vote!, ct);
        if (vote!.OptIn is { } optIn) await customers.UpsertAsync(optIn, ct);
        return (null, await ViewAsync(pollId, ct));
    }

    private PollView View(Ballot p) => new(
        p.Id, p.Title, p.Body, p.AllowMultiple, p.RequireContact, p.IsOpen(clock.GetUtcNow().UtcDateTime), VoteRules.People(p.Votes),
        p.Options.OrderBy(o => o.SortOrder).Select(o => new OptionResult(o.Id, o.Label, VoteRules.PeopleFor(p.Votes, o.Id))).ToList(),
        VoteRules.OptInText, VoteRules.Privacy, CustomerRules.SignUpMeans);
}
