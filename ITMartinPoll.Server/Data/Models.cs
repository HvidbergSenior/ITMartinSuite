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
    public int        Id       { get; set; }
    public int        PollId   { get; set; }
    public int        OptionId { get; set; }
    public string     Comment  { get; set; } = "";
    public DateTime   VotedAt  { get; set; } = DateTime.UtcNow;
    public Poll       Poll     { get; set; } = null!;
    public PollOption Option   { get; set; } = null!;
}

public class ImageSession
{
    public int       Id             { get; set; }
    public string    Title          { get; set; } = "";
    public string    Description    { get; set; } = "";
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
