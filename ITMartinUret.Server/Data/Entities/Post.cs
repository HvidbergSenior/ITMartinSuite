namespace ITMartinUret.Server.Data.Entities;

public enum PostStatus { Visible, Hidden, Deleted }

public class Post
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Secret capability token — the only way the poster can manage the post later. No login system.
    public Guid EditToken { get; set; } = Guid.NewGuid();

    public string Company { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string? ContactEmail { get; set; }
    public PostStatus Status { get; set; } = PostStatus.Visible;

    public string TosVersion { get; set; } = "";
    public DateTime TosAcceptedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // AI-generated "what can you do about this" suggestions — general guidance, not legal advice.
    // Generated on demand by the poster, shown publicly (clearly labelled) since it may help
    // others in a similar situation, not just the original poster.
    public string? ActionSuggestions { get; set; }
    public DateTime? ActionSuggestionsGeneratedAt { get; set; }

    public List<PostUpdate> Updates { get; set; } = [];
    public List<Attachment> Attachments { get; set; } = [];
}
