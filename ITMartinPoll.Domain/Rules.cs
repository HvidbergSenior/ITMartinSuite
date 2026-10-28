namespace ITMartinPoll.Domain;

// The rules of Stem's other poll kinds (moved out of the Razor pages 2026-10-12). They work on the loaded aggregates.

public static class DatePollRules
{
    /// <summary>"Vibz, Eigil, Bertil" -> one name each; one person can answer for the household in a single click.</summary>
    public static List<string> ParseNames(string? input) =>
        (input ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public static bool IsOpen(DatePoll p) => !p.HasDeadlinePassed;

    /// <summary>Yes/Maybe/No for every name on one date.</summary>
    public static void Respond(DatePoll p, int dateId, IReadOnlyList<string> names, string status, DateTime nowUtc)
    {
        var date = p.Dates.FirstOrDefault(d => d.Id == dateId);
        if (date is null || names.Count == 0 || !IsOpen(p)) return;
        foreach (var name in names)
        {
            var r = date.Responses.FirstOrDefault(x => x.VoterName == name);
            if (r is null) date.Responses.Add(new DatePollResponse { DateId = dateId, VoterName = name, Status = status });
            else { r.Status = status; r.RespondedAt = nowUtc; }
        }
    }

    // Exclusive across the whole poll and independent of Status - "which would I actually want", not "which can I attend".
    // Toggling the preferred date again clears it.
    public static void TogglePreferred(DatePoll p, int dateId, IReadOnlyList<string> names)
    {
        var date = p.Dates.FirstOrDefault(d => d.Id == dateId);
        if (date is null || names.Count == 0 || !IsOpen(p)) return;
        var primary = names[0];
        var alreadyPreferred = date.Responses.Any(r => r.VoterName == primary && r.IsPreferred);
        foreach (var r in p.Dates.SelectMany(d => d.Responses).Where(r => names.Contains(r.VoterName))) r.IsPreferred = false;
        if (alreadyPreferred) return;
        foreach (var name in names)
        {
            var r = date.Responses.FirstOrDefault(x => x.VoterName == name);
            if (r is null) date.Responses.Add(new DatePollResponse { DateId = dateId, VoterName = name, Status = "Maybe", IsPreferred = true });
            else r.IsPreferred = true;
        }
    }

    /// <summary>A comment only sticks to answers that exist (pick Yes/Maybe/No first).</summary>
    public static void SetComment(DatePoll p, int dateId, IReadOnlyList<string> names, string comment)
    {
        if (!IsOpen(p)) return;
        var date = p.Dates.FirstOrDefault(d => d.Id == dateId);
        foreach (var r in date?.Responses.Where(r => names.Contains(r.VoterName)) ?? []) r.Comment = comment.Trim();
    }

    /// <returns>Error (Danish) or null when the date was added.</returns>
    public static string? SuggestDate(DatePoll p, DateTime date)
    {
        if (!IsOpen(p)) return "Afstemningen er lukket.";
        if (p.Dates.Any(d => d.Date.Date == date.Date)) return "Den dato er allerede på listen";
        p.Dates.Add(new DatePollDate { DatePollId = p.Id, Date = date.Date, SortOrder = p.Dates.Count == 0 ? 0 : p.Dates.Max(d => d.SortOrder) + 1 });
        return null;
    }

    /// <summary>"Jeg vil gerne være vært" on, with a place - or off.</summary>
    public static void SetHost(DatePoll p, string name, bool host, string place)
    {
        var row = p.Hosts.FirstOrDefault(h => h.VoterName == name);
        if (!host) { if (row is not null) p.Hosts.Remove(row); return; }
        if (row is null) p.Hosts.Add(row = new DatePollHost { DatePollId = p.Id, VoterName = name });
        var pl = place.Trim();
        row.Place = pl.Length > 120 ? pl[..120] : pl;
    }

    public static void Chat(DatePoll p, string? name, string text) =>
        p.ChatMessages.Add(new DatePollChatMessage { DatePollId = p.Id, SenderName = string.IsNullOrWhiteSpace(name) ? "Anonym" : name.Trim(), Text = text.Trim() });
}

/// <summary>Who answers a package: the name label ("Martin 222", or just "Mads" on a link), or - when the link lets you
/// answer without a name (user 2026-10-28) - the phone's own token. A named answer is found again by its name, as always.</summary>
public sealed record Voter(string Label, string Token)
{
    public bool Owns(Vote v) => Label.Length > 0 ? v.VoterName == Label : Token.Length > 0 && v.VoterName == "" && v.VoterToken == Token;

    public Vote NewVote(Poll poll, int optionId, DateTime nowUtc) =>
        new() { PollId = poll.Id, OptionId = optionId, VoterName = Label, VoterToken = Token, VotedAt = nowUtc };

    /// <summary>The same person across questions (for counting people): the name, or the phone.</summary>
    public static string Key(Vote v) => v.VoterName != "" ? v.VoterName : "~" + v.VoterToken;
}

public static class PackageRules
{
    // Vote has no voter token on old rows, so a household is "name + house number" - that is why the number is mandatory
    // there: two people called Martin would otherwise overwrite each other's answer.
    public static string VoterLabel(string name, string house) => $"{name.Trim()} {house.Trim()}".Trim();

    public static bool IsIdentified(string name, string house) => !string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(house);

    /// <summary>A package opened by link (friends, a family - FezBoyz 2026-10-21) asks for a name only, and the name is
    /// optional there (user 2026-10-28); the household number belongs to the forening packages (Skelagerhøjen), where both
    /// are needed.</summary>
    public static bool IsIdentified(string name, string house, bool needsHouse) =>
        needsHouse ? IsIdentified(name, house) : true;

    /// <summary>One answer per person per question; choosing again changes it. A question that allows several answers
    /// (user 2026-10-21: "also choose more things") keeps each one, and a second tap on the same answer takes it back -
    /// with its own comment and star. A comment written before the first choice comes with it.</summary>
    public static void Choose(Poll poll, Voter voter, int optionId, string pendingComment, DateTime nowUtc)
    {
        var mine = poll.Votes.Where(voter.Owns).ToList();
        if (poll.AllowMultiple)
        {
            if (mine.FirstOrDefault(v => v.OptionId == optionId) is { } same)
            {
                poll.Votes.Remove(same);
                // "Vælg dage" keeps one comment for the person - move it to a day that is left. A comment on an answer goes with it.
                if (poll.Kind == QuestionKinds.Days && same.Comment != "" && mine.FirstOrDefault(v => v != same) is { } other && other.Comment == "")
                    other.Comment = same.Comment;
                // a star needs at least two answers to choose between
                if (mine.Count <= 2) foreach (var left in mine) left.Priority = 0;
                return;
            }
            var added = voter.NewVote(poll, optionId, nowUtc);
            added.Comment = mine.Count == 0 ? pendingComment : "";
            poll.Votes.Add(added);
            return;
        }
        var v = mine.FirstOrDefault();
        if (v is null) { var first = voter.NewVote(poll, optionId, nowUtc); first.Comment = pendingComment; poll.Votes.Add(first); }
        else { v.OptionId = optionId; v.VotedAt = nowUtc; }
    }

    /// <summary>"Giv karakter": 1-5 for one answer; the same score again takes it back, another score changes it.</summary>
    public static void Grade(Poll poll, Voter voter, int optionId, int score, DateTime nowUtc)
    {
        if (score is < 1 or > QuestionKinds.MaxScore || poll.Options.All(o => o.Id != optionId)) return;
        var v = poll.Votes.FirstOrDefault(x => voter.Owns(x) && x.OptionId == optionId);
        if (v is null) { var n = voter.NewVote(poll, optionId, nowUtc); n.Score = score; poll.Votes.Add(n); }
        else if (v.Score == score) { if (v.Comment == "") poll.Votes.Remove(v); else v.Score = 0; }
        else { v.Score = score; v.VotedAt = nowUtc; }
    }

    /// <summary>A comment on one answer: "Giv karakter" (user 2026-10-21: "people can comment on each and make grades") -
    /// a comment without a score yet is a row with Score 0, not counted in the average - and, since 2026-10-28, a chosen
    /// answer in "Vælg svar" ("comments per choice").</summary>
    /// <returns>False when there is nothing to put it on (an answer that is not chosen).</returns>
    public static bool OptionComment(Poll poll, Voter voter, int optionId, string comment, DateTime nowUtc)
    {
        if (poll.Options.All(o => o.Id != optionId)) return false;
        var c = comment.Trim();
        if (c.Length > 1000) c = c[..1000];
        var v = poll.Votes.FirstOrDefault(x => voter.Owns(x) && x.OptionId == optionId);
        if (v is null)
        {
            if (poll.Kind != QuestionKinds.Grade || c == "") return false;
            var n = voter.NewVote(poll, optionId, nowUtc); n.Comment = c; poll.Votes.Add(n);
        }
        else if (c == "" && v.Score == 0 && poll.Kind == QuestionKinds.Grade) poll.Votes.Remove(v);
        else v.Comment = c;
        return true;
    }

    /// <summary>"Vælg dage" (and old answers): one comment for the person on the question.</summary>
    /// <returns>False when there is no answer yet (the comment is then kept and saved with the choice).</returns>
    public static bool Comment(Poll poll, Voter voter, string comment)
    {
        var v = poll.Votes.FirstOrDefault(voter.Owns);
        if (v is null) return false;
        v.Comment = comment.Trim();
        return true;
    }

    /// <summary>First priority (user 2026-10-28): among several chosen answers, one gets the star; tapping it again takes
    /// the star back. Only on a question where several may be chosen, only on an answer you chose.</summary>
    public static void SetFirst(Poll poll, Voter voter, int optionId)
    {
        if (!poll.AllowMultiple) return;
        var mine = poll.Votes.Where(voter.Owns).ToList();
        if (mine.FirstOrDefault(v => v.OptionId == optionId) is not { } target) return;
        var on = target.Priority != 1;
        foreach (var v in mine) v.Priority = 0;
        if (on) target.Priority = 1;
    }

    /// <summary>How many people put this answer first.</summary>
    public static int FirstCount(Poll poll, int optionId) => poll.Votes.Count(v => v.OptionId == optionId && v.Priority == 1);
}

public static class RatingRules
{
    /// <summary>One rating per image per browser (token); rating again changes it.</summary>
    public static void Rate(ImageSession s, int imageId, string token, string voterName, int score, string? comment, DateTime nowUtc)
    {
        var img = s.Images.FirstOrDefault(i => i.Id == imageId);
        if (img is null || score is < 1 or > 5 || token.Length == 0) return;
        var r = img.Ratings.FirstOrDefault(x => x.VoterToken == token);
        if (r is null) img.Ratings.Add(r = new ImageRating { ImageId = imageId, VoterToken = token, Comment = (comment ?? "").Trim() });
        else if (comment is not null) r.Comment = comment.Trim();
        r.Score = score; r.VoterName = voterName.Trim(); r.RatedAt = nowUtc;
    }

    public static bool Comment(ImageSession s, int imageId, string token, string comment)
    {
        var r = s.Images.FirstOrDefault(i => i.Id == imageId)?.Ratings.FirstOrDefault(x => x.VoterToken == token);
        if (r is null) return false;
        r.Comment = comment.Trim();
        return true;
    }
}

public static class HelpRules
{
    // SortOrder 50-99 = things people bring (car, boxes, tools); below 50 and guest-added (100+) = jobs.
    public static bool IsBring(HelpTask t) => t.SortOrder is >= 50 and < 100;

    /// <summary>A task a guest adds is saved at once, so everyone else can tick it too. Same label = the same task.</summary>
    public static HelpTask? AddTask(HelpEvent e, string label, string addedBy)
    {
        var l = label.Trim();
        if (l.Length < 2 || !e.IsOpen) return null;
        var t = e.Tasks.FirstOrDefault(x => x.Label.Equals(l, StringComparison.OrdinalIgnoreCase));
        if (t is null) e.Tasks.Add(t = new HelpTask { HelpEventId = e.Id, Label = l, SortOrder = 100, AddedBy = addedBy.Trim() });
        return t;
    }

    /// <returns>Error (Danish) or null when signed up.</returns>
    public static string? SignUp(HelpEvent e, string name, string phone, string comment, IReadOnlyCollection<int> slots, IReadOnlyCollection<int> tasks)
    {
        if (!e.IsOpen) return "Tilmeldingen er lukket.";
        if (name.Trim().Length < 2) return "Skriv dit navn, så vi ved hvem der kommer.";
        if (slots.Count == 0) return "Sæt kryds ved mindst én tid.";
        e.Signups.Add(new HelpSignup
        {
            HelpEventId = e.Id, Name = name.Trim(), Phone = phone.Trim(), Comment = comment.Trim(),
            SlotIds = string.Join(',', slots), TaskIds = string.Join(',', tasks),
        });
        return null;
    }

    public static void Chat(HelpEvent e, string name, string text, bool organiser)
    {
        if (e.IsOpen || organiser) e.Chat.Add(new HelpChatMessage { HelpEventId = e.Id, Name = name.Trim(), Text = text.Trim(), IsOrganiser = organiser });
    }
}
