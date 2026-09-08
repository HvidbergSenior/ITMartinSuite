namespace ITMartinPolstrer.Server.Data.Entities;

// One piece of furniture she's reupholstering. Instructions come from
// someone else (a customer, in person or by message) and are easy to
// forget - Instructions is the whole point of this app: write down what
// you were told the moment you're told it. Photos/videos never touch this
// entity or this app's own storage - they go straight to the NAS gallery
// (see NasPhotoService), keyed off Slug.
public sealed class Job
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDone { get; set; }

    // Folder name for this job's photos on the NAS - derived from Title at
    // creation time and never changed afterward, even if Title is edited
    // later, so existing NAS photos don't get orphaned from a rename.
    public string Slug { get; set; } = string.Empty;
}
