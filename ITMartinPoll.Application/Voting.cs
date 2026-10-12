using ITMartinPoll.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinPoll.Application;

public interface ICustomerStore
{
    /// <summary>Marks the customer as unsubscribed. False if the e-mail is not in the Kundekartotek.</summary>
    Task<bool> UnsubscribeAsync(string email, DateTime nowUtc, CancellationToken ct = default);
    /// <summary>Adds or updates a customer who just said yes (votes do this inside IVotingStore.SaveAsync).</summary>
    Task UpsertAsync(CustomerOptIn optIn, CancellationToken ct = default);
}

// The Kundekartotek: sign up (from other sites, e.g. "prøv gratis" on itmartin.dk) and leave with one e-mail.
public sealed class CustomerList(ICustomerStore store, TimeProvider clock)
{
    public async Task<string?> SignUpAsync(string? email, string? name, string source, string consentText, string interests, CancellationToken ct = default)
    {
        var e = CustomerRules.Normalize(email);
        if (!VoteRules.IsRealEmail(e)) return "Skriv en rigtig e-mail, fx navn@gmail.com.";
        await store.UpsertAsync(new CustomerOptIn(e, (name ?? "").Trim(), source, consentText, interests, clock.GetUtcNow().UtcDateTime), ct);
        return null;
    }

    /// <returns>True when the e-mail was found and is now unsubscribed.</returns>
    public Task<bool> UnsubscribeAsync(string? email, CancellationToken ct = default) =>
        store.UnsubscribeAsync(CustomerRules.Normalize(email), clock.GetUtcNow().UtcDateTime, ct);
}

public interface IVotingStore
{
    Task<Ballot?> GetAsync(int pollId, CancellationToken ct = default);
    /// <summary>Stores the vote rows and, if given, adds/updates the customer opt-in - in one transaction.</summary>
    Task SaveAsync(int pollId, AcceptedVote vote, CancellationToken ct = default);
}

// Voting in Stem: the poll page and other sites (bogshoppen.dk via /api/polls) go through here,
// so the rules (open/closed, one or more choices, contact, one vote per e-mail, opt-in) live in one place.
public sealed class Voting(IVotingStore store, TimeProvider clock)
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
        return (null, await ViewAsync(pollId, ct));
    }

    private PollView View(Ballot p) => new(
        p.Id, p.Title, p.Body, p.AllowMultiple, p.RequireContact, p.IsOpen(clock.GetUtcNow().UtcDateTime), VoteRules.People(p.Votes),
        p.Options.OrderBy(o => o.SortOrder).Select(o => new OptionResult(o.Id, o.Label, VoteRules.PeopleFor(p.Votes, o.Id))).ToList(),
        VoteRules.OptInText, VoteRules.Privacy, CustomerRules.SignUpMeans);
}

public static class DependencyInjection
{
    /// <summary>The use cases. The store comes from AddPollInfrastructure.</summary>
    public static IServiceCollection AddPollApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<Voting>();
        services.AddScoped<CustomerList>();
        return services;
    }
}
