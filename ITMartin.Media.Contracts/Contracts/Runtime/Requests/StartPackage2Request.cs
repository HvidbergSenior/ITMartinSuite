using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Requests;

public sealed class StartPackage2Request
{
    public required string SourceLibraryPath { get; init; }

    public EnhancementProfile Profile { get; init; }
        = EnhancementProfile.Archival;

    public bool EnableAiEnhancement { get; init; }

    public bool EnableUpscaling { get; init; }

    public bool EnableFrameInterpolation { get; init; }
}