namespace ITMartinAdhd.Application.Models;

public sealed class StoredItemModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Location { get; set; } = "";
    public string? Notes { get; set; }
    public DateTime StoredAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public string TimeAgo
    {
        get
        {
            var diff = DateTime.UtcNow - UpdatedAt;
            if (diff.TotalMinutes < 1) return "Just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
            return UpdatedAt.ToString("dd/MM/yyyy");
        }
    }
}
