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

    public DeinterlaceMethod
        DeinterlaceMethod
    {
        get;
        set;
    }
        = DeinterlaceMethod.Bwdif;
}