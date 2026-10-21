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

/// <summary>Kunder: is this kundenummer good for Stem's extras (making your own poll)?</summary>
public interface ICustomerCodes
{
    /// <returns>Ok, or an error for the person (Danish).</returns>
    Task<(bool Ok, string Error)> CheckAsync(string code, CancellationToken ct = default);
}

// "Lav din egen afstemning" (/lav, user 2026-10-21): a customer makes simple polls with their kundenummer and sees the results.
public sealed class CustomerPolls(IAggregateStore<Poll> polls, IAggregateStore<Package> packages, ICustomerCodes codes, TimeProvider clock)
{
    /// <summary>One poll or several questions in one (a package). Path = the link to send.</summary>
    public sealed record Mine(int Id, bool IsPackage, string Title, bool Open, int People, List<MineQuestion> Questions)
    {
        public string Path => IsPackage ? $"/pakke/{Id}" : $"/poll/{Id}";
    }
    public sealed record MineQuestion(string Text, List<(string Label, int People)> Results);

    public static string Normalize(string? code) => new((code ?? "").Where(char.IsDigit).ToArray());

    public Task<(bool Ok, string Error)> LoginAsync(string? code, CancellationToken ct = default) => codes.CheckAsync(Normalize(code), ct);

    /// <returns>Error (Danish) or the new poll's id.</returns>
    public async Task<(string? Error, int Id)> CreateAsync(string? code, string title, string body, IEnumerable<string> options,
        bool allowMultiple, DateTime? deadlineUtc, CancellationToken ct = default)
    {
        var (error, mine) = await CreateAsync(code, title, body, [new QuestionDraft(title, options.ToList(), allowMultiple)], deadlineUtc, ct);
        return (error, mine?.Id ?? 0);
    }

    /// <summary>One question = an ordinary poll; several = one link with all of them (a package, answered with a name).</summary>
    /// <returns>Error (Danish) or what was made.</returns>
    public async Task<(string? Error, Mine? Made)> CreateAsync(string? code, string title, string intro, IReadOnlyList<QuestionDraft> questions,
        DateTime? deadlineUtc, CancellationToken ct = default)
    {
        var k = Normalize(code);
        var (ok, error) = await codes.CheckAsync(k, ct);
        if (!ok) return (error, null);
        var now = clock.GetUtcNow().UtcDateTime;
        var open = await OpenCountAsync(k, now, ct);
        var filled = questions.Where(q => q.Text.Trim().Length > 0 || q.Options.Any(o => o.Trim().Length > 0)).ToList();
        // A "karakter" question needs the package page (it is the one that can give 1-5 per answer).
        if (filled.Count <= 1 && filled.All(q => q.Kind != QuestionKinds.Grade))
        {
            var q = filled.FirstOrDefault() ?? new QuestionDraft("", [], false);
            // One question: the title is what it is about, the question itself (when it says more) under it.
            var body = q.Text.Trim() != "" && q.Text.Trim() != title.Trim() ? q.Text.Trim() + (intro.Trim() != "" ? "\n\n" + intro.Trim() : "") : intro;
            var (why, poll) = CustomerPollRules.Make(k, title.Trim() != "" ? title : q.Text, body, q.Options, q.AllowMultiple, deadlineUtc, open, now, q.Kind);
            if (why is not null) return (why, null);
            var p = await polls.AddAsync(poll!, ct);
            return (null, new Mine(p.Id, false, p.Title, true, 0, []));
        }
        var (whyNot, package) = CustomerPollRules.MakeSurvey(k, title, intro, filled, deadlineUtc, open, now);
        if (whyNot is not null) return (whyNot, null);
        var made = await packages.AddAsync(package!, ct);
        return (null, new Mine(made.Id, true, made.Title, true, 0, []));
    }

    // Free: one open of each kind (user 2026-10-21, rule B) - a poll and a poll with several questions are the same kind.
    // Open = active AND not past its end date (a free poll ends by itself - it must not block the next one).
    private async Task<int> OpenCountAsync(string k, DateTime now, CancellationToken ct) =>
        (await polls.ListAsync(ct)).Count(p => p.OwnerCode == k && p.PackageId == null && IsOpen(p.IsActive, p.Deadline, now))
        + (await packages.ListAsync(ct)).Count(p => p.OwnerCode == k && p.IsActive && p.Polls.Any(q => IsOpen(q.IsActive, q.Deadline, now)));

    private static bool IsOpen(bool active, DateTime? deadline, DateTime now) => active && !(deadline is { } d && d.ToUniversalTime() < now);

    public async Task<List<Mine>> MineAsync(string? code, CancellationToken ct = default)
    {
        var k = Normalize(code);
        if (k.Length != 8) return [];
        var now = clock.GetUtcNow().UtcDateTime;
        var all = await polls.ListAsync(ct);
        static MineQuestion Results(Poll p)
        {
            var votes = p.Votes.Select(v => new CastVote(v.OptionId, v.VoterName, v.Email, DateTime.SpecifyKind(v.VotedAt, DateTimeKind.Utc))).ToList();
            if (p.Kind == QuestionKinds.Grade)
                // grades: the average per answer, best first ("Hvilket mærke er pænest?")
                return new MineQuestion(p.Title, p.Options
                    .Select(o => (o, p.Votes.Where(v => v.OptionId == o.Id && v.Score > 0).Select(v => v.Score).ToList()))
                    .OrderByDescending(x => x.Item2.Count == 0 ? 0 : x.Item2.Average())
                    .Select(x => (x.Item2.Count == 0 ? $"{x.o.Label} – ingen karakter endnu" : $"{x.o.Label} – {x.Item2.Average():0.0} af 5", x.Item2.Count)).ToList());
            return new MineQuestion(p.Title, p.Options.OrderBy(o => o.SortOrder).Select(o => (o.Label, VoteRules.PeopleFor(votes, o.Id))).ToList());
        }
        var single = all.Where(p => p.OwnerCode == k && p.PackageId == null).Select(p =>
            new Mine(p.Id, false, p.Title, IsOpen(p.IsActive, p.Deadline, now),
                VoteRules.People(p.Votes.Select(v => new CastVote(v.OptionId, v.VoterName, v.Email, v.VotedAt))), [Results(p)]));
        var several = (await packages.ListAsync(ct)).Where(x => x.OwnerCode == k).Select(x =>
        {
            var qs = all.Where(p => p.PackageId == x.Id).OrderBy(p => p.Id).ToList();
            return new Mine(x.Id, true, x.Title, x.IsActive && qs.Any(q => IsOpen(q.IsActive, q.Deadline, now)),
                qs.SelectMany(q => q.Votes.Select(v => v.VoterName)).Distinct().Count(), qs.Select(Results).ToList());
        });
        return single.Concat(several).OrderByDescending(m => m.Open).ThenByDescending(m => m.Id).ToList();
    }

    /// <summary>Close or open again - only the customer's own.</summary>
    public async Task ToggleAsync(string? code, Mine m, CancellationToken ct = default)
    {
        var k = Normalize(code);
        if (k.Length != 8) return;
        if (m.IsPackage) await packages.ChangeAsync(m.Id, p => { if (p.OwnerCode == k) p.IsActive = !p.IsActive; return Task.CompletedTask; }, ct);
        else await polls.ChangeAsync(m.Id, p => { if (p.OwnerCode == k) p.IsActive = !p.IsActive; return Task.CompletedTask; }, ct);
    }
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
