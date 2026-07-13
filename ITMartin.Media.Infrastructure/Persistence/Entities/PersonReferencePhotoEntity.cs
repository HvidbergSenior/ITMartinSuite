namespace ITMartin.Media.Infrastructure.Persistence.Entities;

public sealed class PersonReferencePhotoEntity
{
    public Guid Id { get; set; }

    public Guid PersonId { get; set; }

    public required string PhotoPath { get; set; }

    /// <summary>512-dim FaceONNX embedding, JSON-serialized float array.</summary>
    public required string EmbeddingJson { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
