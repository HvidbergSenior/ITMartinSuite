using ITMartin.Media.Contracts.Contracts.Runtime.Enums;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class Package2Manifest
{
    public required Guid WorkflowId { get; init; }

    public required Guid PackageId { get; init; }

    public required int FileCount { get; init; }

    public required DateTimeOffset CreatedAtUtc
    {
        get;
        init;
    }

    public required EnhancementProfile EnhancementProfile
    {
        get;
        init;
    }
    public required RestorationProfile RestorationProfile
    {
        get;
        init;
    }
    public IList<EnhancedMediaManifestItem>
        Items { get; init; }
        = [];
}