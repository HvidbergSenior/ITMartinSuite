namespace ITMartin.Media.Infrastructure.Persistence.Entities;

/// <summary>
/// One row per face detected in a library photo (a photo can have several).
/// Presence of any row for a given MediaFilePath means that file's faces have
/// already been indexed, so a re-run of the indexing pass can skip it.
/// </summary>
public sealed class MediaFaceEntity
{
    public Guid Id { get; set; }

    public required string MediaFilePath { get; set; }

    /// <summary>512-dim FaceONNX embedding, JSON-serialized float array.</summary>
    public required string EmbeddingJson { get; set; }

    public Guid? MatchedPersonId { get; set; }

    public double Confidence { get; set; }

    public bool UserConfirmed { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
