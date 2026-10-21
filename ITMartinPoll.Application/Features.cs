using ITMartinPoll.Domain;

namespace ITMartinPoll.Application;

// Date polls: Yes/Maybe/No per date for one or several names, a preferred date, comments, new dates, hosts, chat.
// Every change tells the open pages to refresh.
public sealed class DatePolls(IAggregateStore<DatePoll> store, IImageFiles images, IChangeSignal signal, TimeProvider clock)
{
    public sealed record Form(string Title, string Description, string? ImageName, DateTime? DeadlineUtc, string? Password,
        IReadOnlyList<DateTime> Dates, IReadOnlyList<string> SpreadImages);

    public Task<DatePoll?> GetAsync(int id, CancellationToken ct = default) => store.GetAsync(id, ct);

    public Task RespondAsync(int id, int dateId, IReadOnlyList<string> names, string status, CancellationToken ct = default) =>
        Change(id, p => DatePollRules.Respond(p, dateId, names, status, clock.GetUtcNow().UtcDateTime), ct);

    public Task TogglePreferredAsync(int id, int dateId, IReadOnlyList<string> names, CancellationToken ct = default) =>
        Change(id, p => DatePollRules.TogglePreferred(p, dateId, names), ct);

    public Task SaveCommentAsync(int id, int dateId, IReadOnlyList<string> names, string comment, CancellationToken ct = default) =>
        Change(id, p => DatePollRules.SetComment(p, dateId, names, comment), ct);

    public async Task<string?> SuggestDateAsync(int id, DateTime date, CancellationToken ct = default)
    {
        string? error = "Afstemningen findes ikke længere.";
        await store.ChangeAsync(id, p => { error = DatePollRules.SuggestDate(p, date); return Task.CompletedTask; }, ct);
        if (error is null) signal.Changed(ChangeChannels.DatePoll(id));   // a refused suggestion changes nothing
        return error;
    }

    public Task SetHostAsync(int id, string name, bool host, string place, CancellationToken ct = default) =>
        Change(id, p => DatePollRules.SetHost(p, name, host, place), ct);

    public async Task<List<DatePollChatMessage>> ChatAsync(int id, CancellationToken ct = default) =>
        (await store.GetAsync(id, ct))?.ChatMessages.OrderBy(m => m.SentAt).Take(100).ToList() ?? [];

    public async Task SendChatAsync(int id, string? name, string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        await Change(id, p => { if (p.ChatOpen) DatePollRules.Chat(p, name, text); }, ct);
    }

    // ---- admin ----

    public Task<string> SaveImageAsync(Stream content, string name, CancellationToken ct = default) => images.SaveAsync(content, name, "", ct);

    /// <returns>Error (Danish) or null when saved.</returns>
    public async Task<string?> SaveAsync(int id, Form f, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(f.Title)) return "Titel mangler";
        var dates = f.Dates.Select(d => d.Date).Distinct().OrderBy(d => d).ToList();
        if (dates.Count == 0) return "Tilføj mindst én dato";
        var password = string.IsNullOrWhiteSpace(f.Password) ? null : f.Password.Trim();
        if (id == 0)
        {
            await store.AddAsync(new DatePoll
            {
                Title = f.Title.Trim(), Description = f.Description.Trim(), ImageName = f.ImageName, Deadline = f.DeadlineUtc,
                Password = password, IsActive = true, CreatedAt = clock.GetUtcNow().UtcDateTime,
                Dates = dates.Select((d, i) => new DatePollDate { Date = d, SortOrder = i }).ToList(),
                Images = f.SpreadImages.Select((n, i) => new DatePollImage { FileName = n, SortOrder = i }).ToList(),
            }, ct);
            return null;
        }
        await store.ChangeAsync(id, p =>
        {
            p.Title = f.Title.Trim(); p.Description = f.Description.Trim(); p.ImageName = f.ImageName; p.Deadline = f.DeadlineUtc; p.Password = password;
            p.Images.Clear();
            p.Images.AddRange(f.SpreadImages.Select((n, i) => new DatePollImage { DatePollId = id, FileName = n, SortOrder = i }));
            // Dates are re-keyed rather than diffed when the list changed (rare once answered); their answers go with them.
            if (!p.Dates.OrderBy(d => d.SortOrder).Select(d => d.Date.Date).SequenceEqual(dates))
            {
                p.Dates.Clear();
                p.Dates.AddRange(dates.Select((d, i) => new DatePollDate { DatePollId = id, Date = d, SortOrder = i }));
            }
            return Task.CompletedTask;
        }, ct);
        return null;
    }

    /// <summary>Admin removes a week (with its answers) - never done automatically.</summary>
    public Task RemoveDateAsync(int id, DateTime date, CancellationToken ct = default) =>
        Change(id, p => p.Dates.RemoveAll(d => d.Date == date), ct);

    private async Task Change(int id, Action<DatePoll> change, CancellationToken ct)
    {
        if (await store.ChangeAsync(id, p => { change(p); return Task.CompletedTask; }, ct))
            signal.Changed(ChangeChannels.DatePoll(id));
    }
}

// Packages: several questions + picture ratings answered as one forespørgsel by a household ("name + house number").
public sealed class Packages(IAggregateStore<Package> packages, IAggregateStore<Poll> polls, IAggregateStore<ImageSession> sessions, TimeProvider clock)
{
    public sealed record Answers(Dictionary<int, int> Votes, Dictionary<int, string> VoteComments, Dictionary<int, int> Ratings, Dictionary<int, string> RatingComments);

    /// <summary>The active package with its active questions and series, or null.</summary>
    public async Task<Package?> GetActiveAsync(int id, CancellationToken ct = default)
    {
        var p = await packages.GetAsync(id, ct);
        if (p is null || !p.IsActive) return null;
        p.Polls = p.Polls.Where(x => x.IsActive).ToList();
        p.Sessions = p.Sessions.Where(x => x.IsActive).ToList();
        return p;
    }

    public Task<Package?> GetAsync(int id, CancellationToken ct = default) => packages.GetAsync(id, ct);

    // What this household already said, so a return visit shows their answers instead of a blank form.
    public async Task<Answers> AnswersAsync(Package p, string voterLabel, string token, CancellationToken ct = default)
    {
        var a = new Answers([], [], [], []);
        foreach (var poll in p.Polls)
            if ((await polls.GetAsync(poll.Id, ct))?.Votes.FirstOrDefault(v => v.VoterName == voterLabel) is { } v) { a.Votes[poll.Id] = v.OptionId; a.VoteComments[poll.Id] = v.Comment; }
        foreach (var s in p.Sessions)
            foreach (var img in (await sessions.GetAsync(s.Id, ct))?.Images ?? [])
                if (img.Ratings.FirstOrDefault(r => r.VoterToken == token) is { } r) { a.Ratings[img.Id] = r.Score; a.RatingComments[img.Id] = r.Comment; }
        return a;
    }

    public Task ChooseAsync(int pollId, string voterLabel, int optionId, string pendingComment, CancellationToken ct = default) =>
        polls.ChangeAsync(pollId, p => { PackageRules.Choose(p, voterLabel, optionId, pendingComment, clock.GetUtcNow().UtcDateTime); return Task.CompletedTask; }, ct);

    public async Task<bool> CommentAsync(int pollId, string voterLabel, string comment, CancellationToken ct = default)
    {
        var saved = false;
        await polls.ChangeAsync(pollId, p => { saved = PackageRules.Comment(p, voterLabel, comment); return Task.CompletedTask; }, ct);
        return saved;
    }

    public Task RateAsync(int sessionId, int imageId, string token, string voterLabel, int score, string pendingComment, CancellationToken ct = default) =>
        sessions.ChangeAsync(sessionId, s => { RatingRules.Rate(s, imageId, token, voterLabel, score, null, clock.GetUtcNow().UtcDateTime);
            if (s.Images.FirstOrDefault(i => i.Id == imageId)?.Ratings.FirstOrDefault(r => r.VoterToken == token) is { Comment: "" } r) r.Comment = pendingComment.Trim();
            return Task.CompletedTask; }, ct);

    public async Task<bool> RatingCommentAsync(int sessionId, int imageId, string token, string comment, CancellationToken ct = default)
    {
        var saved = false;
        await sessions.ChangeAsync(sessionId, s => { saved = RatingRules.Comment(s, imageId, token, comment); return Task.CompletedTask; }, ct);
        return saved;
    }
}

// "Giv en hånd" (/tilmeld/{id}): times, jobs and things to bring, sign-up and a chat with the organiser.
public sealed class HelpEvents(IAggregateStore<HelpEvent> store, IChangeSignal signal)
{
    public async Task<HelpEvent?> GetAsync(int id, bool isAdmin, CancellationToken ct = default)
    {
        var e = await store.GetAsync(id, ct);
        if (e is null || (!e.IsActive && !isAdmin)) return null;
        e.Signups = e.Signups.OrderBy(s => s.SignedUpAt).ToList();
        e.Chat = e.Chat.OrderByDescending(m => m.Id).Take(200).Reverse().ToList();
        return e;
    }

    /// <returns>The task's id (an existing one when the label is already there), or null when the label is too short.</returns>
    public async Task<int?> AddTaskAsync(int id, string label, string addedBy, CancellationToken ct = default)
    {
        HelpTask? task = null;
        await store.ChangeAsync(id, e => { task = HelpRules.AddTask(e, label, addedBy); return Task.CompletedTask; }, ct);
        if (task is null) return null;
        signal.Changed(ChangeChannels.Help(id));
        return task.Id;
    }

    /// <returns>Error (Danish) or null when signed up.</returns>
    public async Task<string?> SignUpAsync(int id, string name, string phone, string comment, IReadOnlyCollection<int> slots, IReadOnlyCollection<int> tasks, CancellationToken ct = default)
    {
        string? error = "Arrangementet findes ikke længere.";
        await store.ChangeAsync(id, e => { error = HelpRules.SignUp(e, name, phone, comment, slots, tasks); return Task.CompletedTask; }, ct);
        if (error is null) signal.Changed(ChangeChannels.Help(id));
        return error;
    }

    public async Task SendChatAsync(int id, string name, string text, bool organiser, CancellationToken ct = default)
    {
        if (text.Trim().Length == 0 || name.Trim().Length < 2) return;
        if (await store.ChangeAsync(id, e => { HelpRules.Chat(e, name, text, organiser); return Task.CompletedTask; }, ct))
            signal.Changed(ChangeChannels.Help(id));
    }
}
