namespace ITMartinPolstrer.Core.Data.Entities;

public enum PieceStatus { Modtaget = 0, IGang = 1, Faerdig = 2, Afleveret = 3 }

// One piece of furniture she has taken in. "Instructions" is what she was
// told when she received it - by the customer or by the colleague who
// handed the job over. It is written down on day one and pinned at the top
// of the piece page forever, because that is the thing she forgets.
public sealed class Piece
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Customer { get; set; } = string.Empty;
    public string InstructedBy { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateOnly? Deadline { get; set; }
    public PieceStatus Status { get; set; } = PieceStatus.Modtaget;

    // Comma-separated technique tags ("fjedre, nakkerulle") - this is what
    // makes an old job findable when she meets the same problem again.
    public string Techniques { get; set; } = string.Empty;
    // Who is working on it, what kind of furniture, and the fabric/material.
    // All optional - the phone form must never block on a field.
    public string Worker { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    public List<Comment> Comments { get; set; } = [];

    // URL key (random suffix so it is unguessable on the public /vis page).
    public string Slug { get; set; } = string.Empty;

    // Media folder under the media root, named after the title at creation
    // (see MediaStore.NewFolderFor) and never renamed afterwards.
    public string Folder { get; set; } = string.Empty;

    public List<Step> Steps { get; set; } = [];

    public IEnumerable<string> TechniqueList =>
        Techniques.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

// One entry in the process log: a dated note with the photos/videos taken
// at that moment. The ordered list of steps IS the documentation.
public sealed class Step
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PieceId { get; set; }
    public Piece Piece { get; set; } = null!;
    public DateTime At { get; set; } = DateTime.UtcNow;
    public string Note { get; set; } = string.Empty;
    public List<MediaFile> Media { get; set; } = [];
}

public sealed class MediaFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StepId { get; set; }
    public Step Step { get; set; } = null!;
    // Path relative to the media root, e.g. "laenestol-a1b2c3/20260914-101500-x1y2z3.jpg"
    public string RelativePath { get; set; } = string.Empty;
    // Same, for the generated thumbnail - null for videos.
    public string? ThumbPath { get; set; }
    public bool IsVideo { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

// A remark from anyone in the workshop, written on the board app: "spring
// ordered", "customer called, wants it Friday". Author is free text.
public sealed class Comment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PieceId { get; set; }
    public Piece Piece { get; set; } = null!;
    public DateTime At { get; set; } = DateTime.UtcNow;
    public string Author { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

// The furniture categories offered on the phone form; free text is allowed
// too, these are just the chips.
public static class FurnitureCategories
{
    public static readonly string[] All = ["Stol", "Lænestol", "Sofa", "Skammel / puf", "Bænk", "Sengegavl", "Bil / båd", "Andet"];
}
