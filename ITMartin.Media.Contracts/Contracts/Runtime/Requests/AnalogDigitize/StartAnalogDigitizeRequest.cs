using ITMartin.Media.Contracts.Contracts.Runtime.Enums;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Requests.AnalogDigitize;

public sealed class StartAnalogDigitizeRequest
{
    public required string SourceLibraryPath
    {
        get;
        init;
    }

    public required string WorkingDirectory
    {
        get;
        init;
    }

    // VIDEO

    public bool EnableVideoEnhancement
    {
        get;
        init;
    }

    public bool EnableDeinterlace
    {
        get;
        init;
    }

    public bool EnableCrop
    {
        get;
        init;
    }

    public bool EnableDenoise
    {
        get;
        init;
    }

    public bool EnableColorCorrection
    {
        get;
        init;
    }

    public bool EnableSharpen
    {
        get;
        init;
    }

    public bool EnableStabilization
    {
        get;
        init;
    }

    public bool EnableUpscaling
    {
        get;
        init;
    }

    public bool EnableFrameInterpolation
    {
        get;
        init;
    }

    // AUDIO

    public bool EnableAudioEnhancement
    {
        get;
        init;
    }

    public bool EnableAudioNormalize
    {
        get;
        init;
    }

    public bool EnableAudioNoiseReduction
    {
        get;
        init;
    }

    public bool EnableHumRemoval
    {
        get;
        init;
    }

    public bool EnableAiEnhancement
    {
        get;
        init;
    }

    // IMAGE

    public bool EnableImageEnhancement
    {
        get;
        init;
    }

    // RESTORATION

    public RestorationProfile
        RestorationProfile
    {
        get;
        init;
    }
        = RestorationProfile.Default;
    
    public EnhancementProfile
        EnhancementProfile
    {
        get;
        init;
    }
        = EnhancementProfile.Standard;
    
    public DeinterlaceMethod
        DeinterlaceMethod
    {
        get;
        init;
    }
        = DeinterlaceMethod.Bwdif;

    public int TargetHeight
    {
        get;
        init;
    }
        = 1080;

    public int StabilizationSmoothing
    {
        get;
        init;
    }
        = 10;
}