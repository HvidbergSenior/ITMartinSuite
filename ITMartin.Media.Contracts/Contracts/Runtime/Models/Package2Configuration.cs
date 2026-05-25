using ITMartin.Media.Contracts.Contracts.Runtime.Enums;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class Package2Configuration
{
    public RestorationProfile
        RestorationProfile
    {
        get;
        set;
    }

    public VideoConfiguration Video
    {
        get;
        set;
    } = new();

    public AudioConfiguration Audio
    {
        get;
        set;
    } = new();
}