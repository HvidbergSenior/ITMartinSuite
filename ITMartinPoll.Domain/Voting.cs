using System.Net.Mail;

namespace ITMartinPoll.Domain;

// A poll as seen by someone about to vote: the question, the choices and the votes so far.
public sealed record Ballot(
    int Id, string Title, string Body, bool AllowMultiple, bool RequireContact, bool IsActive, DateTime? DeadlineUtc,
    IReadOnlyList<BallotOption> Options, IReadOnlyList<CastVote> Votes)
{
    public bool IsOpen(DateTime nowUtc) => IsActive && (DeadlineUtc is null || DeadlineUtc.Value > nowUtc);
}

public sealed record BallotOption(int Id, string Label, int SortOrder);

// One row per chosen option (a multi-choice voter has several rows with the same name/e-mail/time).
public sealed record CastVote(int OptionId, string VoterName, string Email, DateTime VotedAtUtc);

// What a voter sends - from the poll page or from bogshoppen.dk.
public sealed record VoteRequest(IReadOnlyCollection<int> OptionIds, string? Name, string? Email, string? Comment, bool AuctionNews);

// The rows to store, and the opt-in for the Kundekartotek when the voter ticked it.
public sealed record AcceptedVote(IReadOnlyList<CastVote> Rows, string Comment, bool WantsAuctionNews, CustomerOptIn? OptIn);

public sealed record CustomerOptIn(string Email, string Name, string Source, string ConsentText, string Interests, DateTime AtUtc);

public static class VoteRules
{
    public const int MaxName = 60, MaxEmail = 120, MaxComment = 280;

    // Stored with each customer as proof of what they agreed to (markedsføringsloven §10, GDPR art. 7).
    // Change the text, and new sign-ups get the new text.
    // 2026-10-12 (user: "make it obvious what they sign up to"): says customer + offers + no newsletters + easy to leave.
    public const string OptInText = "Ja tak – skriv mig op som kunde hos Bogshoppen. Så får jeg et login til liveauktionerne, og I husker, hvad jeg interesserer mig for, så det er nemt for mig at få jeres tilbud. Ingen reklamer, ingen nyhedsbreve – og jeg kan afmelde mig når som helst.";

    // Shown under the e-mail field (GDPR art. 13: who, why, how long, your rights).
    public const string Privacy =
        "INGEN nyhedsbreve – aldrig. Din e-mail vises ikke for andre og bruges kun til at sikre én stemme pr. person. " +
        "Sætter du flueben, bliver du skrevet op som kunde: et login til liveauktionerne, og vi husker dine interesser – intet andet. " +
        "Afmeld når som helst på bogshoppen.dk/afmeld. " +
        "Dataansvarlig: ITMartin (Martin Hvidberg Nielsen), ITMartin@Mensa.dk. Afstemningen slettes senest 6 måneder efter, den er lukket. " +
        "Du kan altid bede om at se eller få slettet dine oplysninger.";

    /// <summary>Checks a vote against the poll's rules. Returns the error to show (Danish), or the rows to store.</summary>
    public static (string? Error, AcceptedVote? Vote) Accept(Ballot poll, VoteRequest req, DateTime nowUtc)
    {
        if (!poll.IsOpen(nowUtc)) return ("Afstemningen er lukket.", null);
        var chosen = req.OptionIds.Distinct().Where(id => poll.Options.Any(o => o.Id == id)).ToList();
        if (chosen.Count == 0) return ("Vælg mindst én mulighed.", null);
        if (!poll.AllowMultiple && chosen.Count > 1) return ("Du kan kun vælge én mulighed.", null);

        var name = Cut((req.Name ?? "").Trim(), MaxName);
        var email = Cut((req.Email ?? "").Trim().ToLowerInvariant(), MaxEmail);
        var comment = Cut((req.Comment ?? "").Trim(), MaxComment);
        if (poll.RequireContact)
        {
            if (name.Length == 0) return ("Skriv dit navn.", null);
            if (!IsRealEmail(email)) return ("Skriv en rigtig e-mail, fx navn@gmail.com.", null);
            if (poll.Votes.Any(v => v.Email == email)) return ("Der er allerede stemt med den e-mail. Man kan kun stemme én gang.", null);
        }
        else email = "";   // never stored when the poll does not ask for it

        var wants = poll.RequireContact && req.AuctionNews;
        var rows = chosen.Select(id => new CastVote(id, name, email, nowUtc)).ToList();
        CustomerOptIn? optIn = null;
        if (wants)
        {
            var picked = string.Join(", ", poll.Options.Where(o => chosen.Contains(o.Id)).OrderBy(o => o.SortOrder).Select(o => o.Label));
            optIn = new CustomerOptIn(email, name, "Stem: " + poll.Title, OptInText, comment.Length == 0 ? picked : picked + " | " + comment, nowUtc);
        }
        return (null, new AcceptedVote(rows, comment, wants, optIn));
    }

    // People, not rows: a multi-choice voter has one row per chosen option.
    public static int People(IEnumerable<CastVote> votes) => votes.Select(VoterKey).Distinct().Count();

    public static int PeopleFor(IEnumerable<CastVote> votes, int optionId) => People(votes.Where(v => v.OptionId == optionId));

    private static string VoterKey(CastVote v) => v.Email != "" ? v.Email : v.VoterName + "|" + v.VotedAtUtc.Ticks;

    public static bool IsRealEmail(string email) =>
        MailAddress.TryCreate(email, out var ma) && ma.Address == email && email.Split('@')[1].Contains('.');

    private static string Cut(string s, int max) => s.Length > max ? s[..max] : s;
}
