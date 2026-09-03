using ITMartin.Media.Contracts.Contracts.Runtime.Enums;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class AnalogDigitizeWorkflowState
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
        Items
    {
        get;
        init;
    }

    public EnhancementProfile EnhancementProfile
    {
        get;
        init;
    } = EnhancementProfile.Standard;

    // VIDEO

    public bool EnableUpscaling
    {
        get;
        set;
    } = false;

    public bool EnableFrameInterpolation
    {
        get;
        set;
    } = false;

    public bool EnableVideoEnhancement
    {
        get;
        set;
    } = true;

    public bool EnableDeinterlace
    {
        get;
        set;
    } = true;

    public bool EnableCrop
    {
        get;
        set;
    } = true;

    public bool EnableDenoise
    {
        get;
        set;
    } = false;

    public bool EnableColorCorrection
    {
        get;
        set;
    } = false;

    public bool EnableSharpen
    {
        get;
        set;
    } = false;

    public bool EnableStabilization
    {
        get;
        set;
    } = false;

    // AUDIO

    public bool EnableAudioEnhancement
    {
        get;
        set;
    } = true;

    public bool EnableAudioNormalize
    {
        get;
        set;
    } = true;

    public bool EnableAudioNoiseReduction
    {
        get;
        set;
    } = false;

    public bool EnableHumRemoval
    {
        get;
        set;
    } = false;

    public bool EnableAiEnhancement
    {
        get;
        set;
    } = false;

    // IMAGE

    public bool EnableImageEnhancement
    {
        get;
        set;
    } = false;

    public DeinterlaceMethod DeinterlaceMethod
    {
        get;
        set;
    } = DeinterlaceMethod.Bwdif;

    public int TargetHeight
    {
        get;
        set;
    } = 1080;

    public int StabilizationSmoothing
    {
        get;
        set;
    } = 10;
    public required AnalogDigitizeConfiguration
        Configuration { get; init; }
    public bool EnableSampleGeneration
    {
        get;
        set;
    }

    public int SampleCount
    {
        get;
        set;
    } = 3;

    public TimeSpan SampleDuration
    {
        get;
        set;
    } = TimeSpan.FromSeconds(30);
    
    public IList<ManualSegment> ManualSegments
    {
        get;
        init;
    } = [];
}