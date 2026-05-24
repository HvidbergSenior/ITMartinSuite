using ITMartin.Media.Contracts.Contracts.Runtime.Enums;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class Package2WorkflowState
{
    public Guid PackageId { get; init; }
    public required string WorkingDirectory
    {
        get;
        init;
    }
    public RestorationProfile
        RestorationProfile
    {
        get;
        set;
    }
        = RestorationProfile.Default;
    public required IList<EnhancedMediaItem>
        Items { get; init; }

    public EnhancementProfile Profile
    {
        get;
        init;
    } = EnhancementProfile.Initial;

    public bool EnableUpscaling
    {
        get;
        set;
    }
    public bool EnableFrameInterpolation
    {
        get;
        set;
    }
    
    public bool EnableAudioEnhancement
    {
        get;
        set;
    }

    public bool EnableVideoEnhancement
    {
        get;
        set;
    }

    public bool EnableImageEnhancement
    {
        get;
        set;
    }
    public bool EnableAiEnhancement
    {
        get;
        set;
    }
}