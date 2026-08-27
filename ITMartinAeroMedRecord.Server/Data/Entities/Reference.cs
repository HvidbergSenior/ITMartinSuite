namespace ITMartinAeroMedRecord.Server.Data.Entities;

// Links a document section to a task and/or a code location - the
// spec-to-implementation traceability this app was originally built for.
// Either AssignmentId or CodeReference (or both) can be set.
public sealed class Reference
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public Guid SectionId { get; set; }
    public Guid? AssignmentId { get; set; }

    // Free text - a repo+file+line, a PR/commit URL, whatever makes sense.
    public string? CodeReference { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DocumentSection Section { get; set; } = null!;
    public Assignment? Assignment { get; set; }
}
