namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class AudioConfiguration
{
    public bool EnableNormalize
    {
        get;
        set;
    } = true;

    public bool EnableNoiseReduction
    {
        get;
        set;
    }

    public bool EnableHumRemoval
    {
        get;
        set;
    }

    public bool EnableAiEnhancement
    {
        get;
        set;
    }

    public bool EnableEnhancement
    {
        get;
        set;
    }

    public bool EnableSpeechEnhancement
    {
        get;
        set;
    }

    public string Codec
    {
        get;
        set;
    } = "aac";
}