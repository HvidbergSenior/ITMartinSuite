namespace ITMartin.Media.Infrastructure.Persistence.Entities;

/// <summary>
/// One row per face detected in a library photo (a photo can have several).
/// Presence of any row for a given RelativePath means that file's faces have
/// already been indexed, so a re-run of the indexing pass can skip it.
/// </summary>
public sealed class MediaFaceEntity
{
    public Guid Id { get; set; }

    public required string MediaFilePath { get; set; }

    /// <summary>
    /// MediaFilePath relative to the library root it was indexed under
    /// (forward slashes, e.g. "Billeder/2012/01-January/SAM_3252.jpg"). The same
    /// physical photo can live at different absolute paths across a NAS
    /// container mount, a local dev copy, and an external-HD export - keying the
    /// "already indexed" check on this instead of the absolute MediaFilePath
    /// means indexing done under any one of those locations is recognized by
    /// the others, instead of each mount triggering its own full re-index.
    /// </summary>
    public required string RelativePath { get; set; }

    /// <summary>512-dim FaceONNX embedding, JSON-serialized float array.</summary>
    public required string EmbeddingJson { get; set; }

    public Guid? MatchedPersonId { get; set; }

    public double Confidence { get; set; }

    public bool UserConfirmed { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
