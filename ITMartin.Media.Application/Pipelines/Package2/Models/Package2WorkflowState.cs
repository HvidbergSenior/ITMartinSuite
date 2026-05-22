
namespace ITMartin.Media.Application.Pipelines.Package2.Models;

public sealed class Package2WorkflowState
{
    public required string PackageId { get; init; }

    public required string WorkingDirectory { get; init; }

    public IList<EnhancedMediaItem> Items { get; init; }
        = [];

    public EnhancementProfile Profile { get; init; }
        = EnhancementProfile.Archival;

    public bool EnableAiEnhancement { get; init; }

    public bool EnableUpscaling { get; init; }

    public bool EnableFrameInterpolation { get; init; }
}