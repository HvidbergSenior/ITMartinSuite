namespace ITMartinKigMed.Domain;

public sealed record Question(Guid Id, string Viewer, string Name, string Text, DateTime At, bool FromMartin = false)
{
    public string Status { get; set; } = QuestionStatus.New;
}

public static class QuestionStatus
{
    public const string New = "ny", Answering = "svarer", Done = "færdig";
}

public sealed record Idea(Guid Id, string Viewer, string Name, string Text, string? Picture, DateTime At)
{
    public bool Shown { get; set; }   // Martin chose "Vis for alle"
}

public sealed class Poll
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Text { get; init; } = "";
    public List<string> Options { get; init; } = [];
    public Dictionary<string, int> Votes { get; init; } = [];   // viewer key -> option index
    public bool Open { get; set; } = true;
    public int[] Counts() => Options.Select((_, i) => Votes.Values.Count(v => v == i)).ToArray();
}

// One live session: Martin in the studio, viewers on the front page. The shape is also the saved session.json,
// so the property names stay as they are; every change goes through the methods below.
public sealed class LiveSession
{
    public const int MaxQuestions = 500, MaxIdeas = 200, MaxIdeasPerViewer = 10, MaxText = 400, MaxPollOptions = 6;
    public static readonly string[] Emojis = ["👍", "❤️", "😂", "🔥", "👏", "🎸", "🤯", "🐦"];

    public bool Live { get; set; }
    public string Title { get; set; } = "";
    public string Next { get; set; } = "";   // "Næste gang: torsdag kl. 20" - shown while offline
    public DateTime? StartedAt { get; set; }
    public List<Question> Questions { get; init; } = [];
    public List<Idea> Ideas { get; init; } = [];
    public List<Poll> Polls { get; init; } = [];
    public Dictionary<string, int> Reactions { get; init; } = [];

    // Going live after a break starts a clean session; pressing it again while live only changes the title.
    public void GoLive(string title, DateTime now)
    {
        if (!Live) { Questions.Clear(); Ideas.Clear(); Polls.Clear(); Reactions.Clear(); StartedAt = now; }
        Live = true;
        Title = Texts.Clip(title, 120);
    }

    // false = was not live (nothing to end or archive).
    public bool End()
    {
        if (!Live) return false;
        Live = false;
        return true;
    }

    public string ArchiveName(DateTime now) => (StartedAt ?? now).ToString("yyyy-MM-dd_HHmm") + ".json";

    public void SetTexts(string title, string next) { Title = Texts.Clip(title, 120); Next = Texts.Clip(next, 200); }

    public bool Ask(string viewer, string name, string text, DateTime now, bool fromMartin = false)
    {
        text = Texts.Clip(text, MaxText);
        if (text.Length == 0) return false;
        Questions.Add(new Question(Guid.NewGuid(), viewer, fromMartin ? "Martin" : Texts.NameOf(name), text, now, fromMartin));
        if (Questions.Count > MaxQuestions) Questions.RemoveAt(0);
        return true;
    }

    public void SetStatus(Guid id, string status)
    {
        // Only one question is "svarer nu" at a time.
        if (status == QuestionStatus.Answering)
            foreach (var q in Questions.Where(q => q.Status == QuestionStatus.Answering)) q.Status = QuestionStatus.Done;
        if (Questions.FirstOrDefault(q => q.Id == id) is { } hit) hit.Status = status;
    }

    public void DeleteQuestion(Guid id) => Questions.RemoveAll(q => q.Id == id);

    public bool SendIdea(string viewer, string name, string text, string? picture, DateTime now)
    {
        text = Texts.Clip(text, MaxText);
        if (text.Length == 0 && picture is null) return false;
        if (Ideas.Count(i => i.Viewer == viewer) >= MaxIdeasPerViewer || Ideas.Count >= MaxIdeas) return false;
        Ideas.Add(new Idea(Guid.NewGuid(), viewer, Texts.NameOf(name), text, picture, now));
        return true;
    }

    public void ShowIdea(Guid id, bool shown) { if (Ideas.FirstOrDefault(i => i.Id == id) is { } hit) hit.Shown = shown; }
    public void DeleteIdea(Guid id) => Ideas.RemoveAll(i => i.Id == id);

    // A picture everybody may see: one Martin has shown with "Vis for alle".
    public bool PictureIsShown(string picture) => Ideas.Any(i => i.Picture == picture && i.Shown);

    // A new poll closes the old one. Needs two answers.
    public bool StartPoll(string text, IEnumerable<string> options)
    {
        var opts = options.Select(o => Texts.Clip(o, 60)).Where(o => o.Length > 0).Take(MaxPollOptions).ToList();
        if (opts.Count < 2) return false;
        ClosePoll();
        Polls.Add(new Poll { Text = Texts.Clip(text, 160), Options = opts });
        return true;
    }

    public void ClosePoll() { foreach (var p in Polls) p.Open = false; }

    // Changing your mind is allowed, one vote per phone.
    public bool Vote(string viewer, Guid poll, int option)
    {
        if (Polls.FirstOrDefault(p => p.Id == poll && p.Open) is not { } p || option < 0 || option >= p.Options.Count) return false;
        p.Votes[viewer] = option;
        return true;
    }

    public bool React(string emoji)
    {
        if (!Emojis.Contains(emoji)) return false;
        Reactions[emoji] = Reactions.GetValueOrDefault(emoji) + 1;
        return true;
    }
}

public static class Texts
{
    public static string Clip(string? s, int max) { s = (s ?? "").Trim(); return s.Length > max ? s[..max] : s; }
    public static string NameOf(string? name) => Clip(name, 40) is { Length: > 0 } n ? n : "Gæst";
}
