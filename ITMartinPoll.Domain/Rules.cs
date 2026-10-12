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

public static class PackageRules
{
    // Vote has no voter token, so a household is "name + house number" - that is why the number is mandatory:
    // two people called Martin would otherwise overwrite each other's answer.
    public static string VoterLabel(string name, string house) => $"{name.Trim()} {house.Trim()}".Trim();

    public static bool IsIdentified(string name, string house) => !string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(house);

    /// <summary>One answer per household per question; choosing again changes it. A comment written before the choice comes with it.</summary>
    public static void Choose(Poll poll, string voterLabel, int optionId, string pendingComment, DateTime nowUtc)
    {
        var v = poll.Votes.FirstOrDefault(x => x.VoterName == voterLabel);
        if (v is null) poll.Votes.Add(new Vote { PollId = poll.Id, OptionId = optionId, VoterName = voterLabel, Comment = pendingComment, VotedAt = nowUtc });
        else { v.OptionId = optionId; v.VotedAt = nowUtc; }
    }

    /// <returns>False when there is no answer yet (the comment is then kept and saved with the choice).</returns>
    public static bool Comment(Poll poll, string voterLabel, string comment)
    {
        var v = poll.Votes.FirstOrDefault(x => x.VoterName == voterLabel);
        if (v is null) return false;
        v.Comment = comment.Trim();
        return true;
    }
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
        if (l.Length < 2) return null;
        var t = e.Tasks.FirstOrDefault(x => x.Label.Equals(l, StringComparison.OrdinalIgnoreCase));
        if (t is null) e.Tasks.Add(t = new HelpTask { HelpEventId = e.Id, Label = l, SortOrder = 100, AddedBy = addedBy.Trim() });
        return t;
    }

    /// <returns>Error (Danish) or null when signed up.</returns>
    public static string? SignUp(HelpEvent e, string name, string phone, string comment, IReadOnlyCollection<int> slots, IReadOnlyCollection<int> tasks)
    {
        if (name.Trim().Length < 2) return "Skriv dit navn, så vi ved hvem der kommer.";
        if (slots.Count == 0) return "Sæt kryds ved mindst én tid.";
        e.Signups.Add(new HelpSignup
        {
            HelpEventId = e.Id, Name = name.Trim(), Phone = phone.Trim(), Comment = comment.Trim(),
            SlotIds = string.Join(',', slots), TaskIds = string.Join(',', tasks),
        });
        return null;
    }

    public static void Chat(HelpEvent e, string name, string text, bool organiser) =>
        e.Chat.Add(new HelpChatMessage { HelpEventId = e.Id, Name = name.Trim(), Text = text.Trim(), IsOrganiser = organiser });
}
