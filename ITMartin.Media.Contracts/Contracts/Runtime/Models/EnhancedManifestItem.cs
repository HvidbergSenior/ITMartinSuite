namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class EnhancedMediaManifestItem
{
    public required string OriginalPath { get; init; }

    public required string NormalizedPath { get; init; }

    public required string EnhancedPath { get; init; }

    public required MediaKind MediaKind { get; init; }

    public IList<EnhancementOperation> Operations { get; init; }
        = [];
}