namespace ITMartinPoll.Server.Data;

public static class RatingScale
{
    public static readonly string[] Labels =
        ["", "Dårlig", "Svag", "Nogenlunde", "God, men kan forbedres", "Perfekt som den er"];
    public static string Label(int score) => score is >= 1 and <= 5 ? Labels[score] : "";
}

public class Poll
{
    public int       Id        { get; set; }
    public string    Title     { get; set; } = "";
    public string    Body      { get; set; } = "";
    public string?   ImageName { get; set; }
    public DateTime  CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? Deadline  { get; set; }
    public bool      IsActive  { get; set; } = true;

    public List<PollOption> Options { get; set; } = [];
    public List<Vote>       Votes   { get; set; } = [];

    public bool HasDeadlinePassed =>
        Deadline.HasValue && Deadline.Value.ToUniversalTime() < DateTime.UtcNow;
}

public class PollOption
{
    public int    Id        { get; set; }
    public int    PollId    { get; set; }
    public string Label     { get; set; } = "";
    public int    SortOrder { get; set; }
    public Poll   Poll      { get; set; } = null!;
    public List<Vote> Votes { get; set; } = [];
}

public class Vote
{
    public int        Id        { get; set; }
    public int        PollId    { get; set; }
    public int        OptionId  { get; set; }
    public string     Comment   { get; set; } = "";
    public string     VoterName { get; set; } = "";
    public DateTime   VotedAt   { get; set; } = DateTime.UtcNow;
    public Poll       Poll      { get; set; } = null!;
    public PollOption Option    { get; set; } = null!;
}

public class ImageSession
{
    public int       Id             { get; set; }
    public string    Title          { get; set; } = "";
    public string    Description    { get; set; } = "";

    // Shown to voters above each image, e.g. "Hvilket billede kan du bedst lide?" — falls
    // back to a generic prompt in SessionPage.razor when left blank.
    public string    Question       { get; set; } = "";

    public string?   CoverImageName { get; set; }
    public bool      IsActive       { get; set; } = true;
    public DateTime  CreatedAt      { get; set; } = DateTime.UtcNow;
    public DateTime? Deadline       { get; set; }
    public List<SessionImage> Images { get; set; } = [];

    public bool HasDeadlinePassed =>
        Deadline.HasValue && Deadline.Value.ToUniversalTime() < DateTime.UtcNow;
}

public class SessionImage
{
    public int    Id        { get; set; }
    public int    SessionId { get; set; }
    public string FileName  { get; set; } = "";
    public int    SortOrder { get; set; }
    public List<ImageRating> Ratings { get; set; } = [];
    public ImageSession Session { get; set; } = null!;
}

public class ImageRating
{
    public int      Id         { get; set; }
    public int      ImageId    { get; set; }
    public int      Score      { get; set; }
    public string   VoterToken { get; set; } = "";
    public string   VoterName  { get; set; } = "";
    public string   Comment    { get; set; } = "";
    public DateTime RatedAt    { get; set; } = DateTime.UtcNow;
    public SessionImage Image  { get; set; } = null!;
}

// A "which day(s) work?" poll - unlike Poll (one choice, one-and-done), every
// voter can answer Yes/No/Maybe on every date and come back to change their
// mind, since availability polls live over days/weeks while people's plans
// shift.
public class DatePoll
{
    public int       Id          { get; set; }
    public string     Title       { get; set; } = "";
    public string     Description { get; set; } = "";
    public string?    ImageName   { get; set; }
    public DateTime   CreatedAt   { get; set; } = DateTime.UtcNow;
    public DateTime?  Deadline    { get; set; }
    public bool       IsActive    { get; set; } = true;

    // Optional, plain-text comparison - same trust level as the admin PIN
    // (Poll:AdminPin), proportionate to what this actually protects (a
    // family/friends scheduling poll, not sensitive data). Null/empty means
    // no password required, same as today.
    public string?    Password    { get; set; }

    public List<DatePollDate>        Dates        { get; set; } = [];
    public List<DatePollChatMessage> ChatMessages { get; set; } = [];
    public List<DatePollImage>       Images       { get; set; } = [];

    public bool HasDeadlinePassed =>
        Deadline.HasValue && Deadline.Value.ToUniversalTime() < DateTime.UtcNow;

    // The chat is a live discussion about scheduling, not an archive - once the
    // poll closes there's nothing left to coordinate, so close the chat with it
    // rather than leave a stale input box on a decided poll.
    public bool ChatOpen => IsActive && !HasDeadlinePassed;
}

public class DatePollDate
{
    public int      Id         { get; set; }
    public int      DatePollId { get; set; }
    public DateTime Date       { get; set; }
    public int      SortOrder  { get; set; }

    public DatePoll Poll { get; set; } = null!;
    public List<DatePollResponse> Responses { get; set; } = [];
}

public class DatePollResponse
{
    public int      Id          { get; set; }
    public int      DateId      { get; set; }
    public string    VoterName   { get; set; } = "";

    // "Yes" / "No" / "Maybe" - free-text rather than an enum column so it reads
    // directly in raw SQL/sqlite browsing without a lookup table, same choice
    // as ReadyCheckResponse.Status in the Club app.
    public string    Status      { get; set; } = "Maybe";
    public string    Comment     { get; set; } = "";
    public DateTime  RespondedAt { get; set; } = DateTime.UtcNow;

    // "If you could only pick one, which week would you actually prefer?" -
    // separate from Status (which is per-date availability), this is a
    // single exclusive pick across every date in the poll: at most one of a
    // voter's responses in a given DatePoll should ever have this true.
    // Independent of availability - someone can prefer a week and still mark
    // it Maybe/No if a conflict didn't stop them wishing it worked out.
    public bool      IsPreferred { get; set; }

    public DatePollDate DateOption { get; set; } = null!;
}

public class DatePollChatMessage
{
    public int      Id         { get; set; }
    public int      DatePollId { get; set; }
    public string   SenderName { get; set; } = "";
    public string   Text       { get; set; } = "";
    public DateTime SentAt     { get; set; } = DateTime.UtcNow;

    public DatePoll Poll { get; set; } = null!;
}

// Extra photos shown interleaved between the date rows on the voter page -
// separate from DatePoll.ImageName (the single hero image at the very top).
// Purely decorative/personal, no relation to any specific date.
public class DatePollImage
{
    public int    Id        { get; set; }
    public int    DatePollId { get; set; }
    public string FileName  { get; set; } = "";
    public int    SortOrder { get; set; }

    public DatePoll Poll { get; set; } = null!;
}
