namespace ITMartin.Media.Contracts.Contracts.Runtime.Requests.Package4;

public sealed class StartPackage4Request
{
    public required string SourceLibraryPath { get; init; }
    public required string WorkingDirectory { get; init; }

    public bool EnableWhiteBalance { get; init; } = true;
    public bool EnableExposureContrast { get; init; } = true;
    public bool EnableSaturationVibrance { get; init; } = true;
    public bool EnableColorGrade { get; init; } = true;
    public bool EnableSharpen { get; init; } = true;
    public bool EnableNoiseReduction { get; init; } = true;
    public bool EnableDeflicker { get; init; } = true;
    public bool EnableStabilization { get; init; } = false;
    public bool EnableStabilizedCrop { get; init; } = false;

    public bool EnableAudioNoiseReduction { get; init; } = true;
    public bool EnableWindNoiseReduction { get; init; } = true;
    public bool EnableHumRemoval { get; init; } = true;
    public bool EnableAudioEq { get; init; } = true;
    public bool EnableDeEss { get; init; } = true;
    public bool EnableAudioCompression { get; init; } = true;
    public bool EnableLoudnessNormalization { get; init; } = true;

    public bool EnableTrim { get; init; } = true;
    public double TrimStartSeconds { get; init; } = 0;
    public double? TrimEndSeconds { get; init; }

    public int DeliveryCrf { get; init; } = 21;
    public int DeliveryMaxRateMbps { get; init; } = 6;
    public string DeliveryAudioBitrate { get; init; } = "160k";
}
