namespace ITMartin.Media.Infrastructure.Persistence.Entities;

// One row per (photo, detected object). A photo the detector looked at and
// found nothing in still gets ONE row with an empty Label, so the index
// knows it was scanned and never re-reads it. See IObjectIndexService.
public sealed class MediaObjectTagEntity
{
    public Guid Id { get; set; }

    public required string MediaFilePath { get; set; }

    /// <summary>Path relative to the library root, forward slashes - the "already scanned" key, same reasoning as MediaFaceEntity.RelativePath.</summary>
    public required string RelativePath { get; set; }

    /// <summary>COCO label ("dog"), or "" for a scanned photo with no objects.</summary>
    public required string Label { get; set; }

    public double Confidence { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
