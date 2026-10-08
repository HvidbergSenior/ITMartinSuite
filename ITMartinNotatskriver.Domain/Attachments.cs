namespace ITMartinNotatskriver.Domain;

/// <summary>What kind of thing the user added to their notes.</summary>
public enum AttachmentKind
{
    /// <summary>A photo or screenshot - already turned upright, shrunk and stripped of GPS/camera data.</summary>
    Image,
    /// <summary>A PDF, sent to the AI as it is.</summary>
    Pdf,
    /// <summary>Text read out of a Word or text file.</summary>
    Text,
}

/// <summary>One thing the user added. Images and PDFs carry bytes; text files carry their text.</summary>
public sealed record Attachment(string Name, AttachmentKind Kind, byte[] Data, string Text)
{
    public static Attachment Image(string name, byte[] jpeg) => new(name, AttachmentKind.Image, jpeg, "");
    public static Attachment Pdf(string name, byte[] pdf) => new(name, AttachmentKind.Pdf, pdf, "");
    public static Attachment FromText(string name, string text) => new(name, AttachmentKind.Text, [], text.Trim());

    /// <summary>Images and PDFs are read by the AI itself, so the user must confirm they hold no CPR or health data.</summary>
    public bool IsVisual => Kind is AttachmentKind.Image or AttachmentKind.Pdf;
}

/// <summary>Tells from a file's name and type what kind of attachment it is - before anything is read.</summary>
public static class AttachmentKinds
{
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".heic", ".webp", ".gif"];
    private static readonly string[] TextExtensions = [".txt", ".md", ".text"];

    /// <returns>The kind, or null when the file cannot be used at all.</returns>
    public static AttachmentKind? Detect(string fileName, string contentType)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        contentType = contentType.ToLowerInvariant();
        if (contentType.StartsWith("image/") || ImageExtensions.Contains(ext)) return AttachmentKind.Image;
        if (ext == ".pdf" || contentType == "application/pdf") return AttachmentKind.Pdf;
        if (ext == ".docx" || TextExtensions.Contains(ext) || contentType.StartsWith("text/")) return AttachmentKind.Text;
        return null;
    }
}
