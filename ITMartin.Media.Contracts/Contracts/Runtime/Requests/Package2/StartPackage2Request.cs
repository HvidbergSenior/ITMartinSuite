using ITMartin.Media.Contracts.Contracts.Runtime.Enums;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Requests.Package2;

public sealed class StartPackage2Request
{
    public required string SourceLibraryPath { get; init; }

    public required string WorkingDirectory { get; init; }

    public bool EnableUpscaling { get; init; }
    public bool EnableFrameInterpolation { get; init; }

    public bool EnableAudioEnhancement { get; init; }

    public bool EnableVideoEnhancement { get; init; }

    public bool EnableImageEnhancement { get; init; }
    public bool EnableAiEnhancement { get; init; }
    public RestorationProfile
        RestorationProfile
    {
        get;
        init;
    }
        = RestorationProfile.Default;
    
}