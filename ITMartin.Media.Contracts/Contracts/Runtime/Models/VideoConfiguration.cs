using ITMartin.Media.Contracts.Contracts.Runtime.Enums;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class VideoConfiguration
{
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
    }

    public bool EnableSharpen
    {
        get;
        set;
    }

    public bool EnableUpscaling
    {
        get;
        set;
    }

    public bool EnableStabilization
    {
        get;
        set;
    }

    public bool EnableColorCorrection
    {
        get;
        set;
    }

    public bool EnableFrameInterpolation
    {
        get;
        set;
    }

    public int TargetHeight
    {
        get;
        set;
    } = 1080;

    public int Crf
    {
        get;
        set;
    } = 18;

    public string Preset
    {
        get;
        set;
    } = "slow";

    public string Codec
    {
        get;
        set;
    } = "libx264";

    public DeinterlaceMethod
        DeinterlaceMethod
    {
        get;
        set;
    }
        = DeinterlaceMethod.Bwdif;
}