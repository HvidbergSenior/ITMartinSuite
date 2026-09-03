namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

// Social/vlog clip enhancement - separate from AnalogDigitize (analog tape
// restoration) because the source material, filter intensities, and finishing
// steps (trim, delivery export) are fundamentally different concerns. Reuses
// AnalogDigitize's EnhancedMediaItem/IVideoEnhancementService/IAudioEnhancementService
// infrastructure rather than duplicating it.
public sealed class Package4WorkflowState
{
    public Guid PackageId { get; init; }

    public required string WorkingDirectory { get; init; }

    public required IList<EnhancedMediaItem> Items { get; init; }

    // Every step below is independently toggleable so a clip can skip
    // whichever isn't relevant (e.g. no wind noise indoors) without touching
    // the others - same "explicit opt-in per step" shape as AnalogDigitize.

    // VIDEO / COLOR
    public bool EnableWhiteBalance { get; set; } = true;
    public bool EnableExposureContrast { get; set; } = true;
    public bool EnableSaturationVibrance { get; set; } = true;
    public bool EnableColorGrade { get; set; } = true;
    public bool EnableSharpen { get; set; } = true;
    public bool EnableNoiseReduction { get; set; } = true;
    public bool EnableDeflicker { get; set; } = true;

    // Off by default - vidstabtransform has a known corruption issue on
    // vertical phone clips (2026-08-24), likely a rotation-metadata mismatch.
    // Leave disabled until that's root-caused; the step still exists so it
    // can be re-enabled per-run once fixed.
    public bool EnableStabilization { get; set; } = false;
    public bool EnableStabilizedCrop { get; set; } = false;
    public int StabilizationSmoothing { get; set; } = 15;

    // AUDIO
    public bool EnableAudioNoiseReduction { get; set; } = true;
    public bool EnableWindNoiseReduction { get; set; } = true;
    public bool EnableHumRemoval { get; set; } = true;
    public bool EnableAudioEq { get; set; } = true;
    public bool EnableDeEss { get; set; } = true;
    public bool EnableAudioCompression { get; set; } = true;
    public bool EnableLoudnessNormalization { get; set; } = true;

    // FINISHING
    public bool EnableTrim { get; set; } = true;
    public double TrimStartSeconds { get; set; } = 0;
    public double? TrimEndSeconds { get; set; }

    public int DeliveryCrf { get; set; } = 21;
    public int DeliveryMaxRateMbps { get; set; } = 6;
    public string DeliveryAudioBitrate { get; set; } = "160k";

    // Set by each checkpoint-producing step (VideoAudioRenderWorkflowStep,
    // TrimDeadFootageWorkflowStep, DeliveryExportWorkflowStep) so the caller
    // can find every intermediate file afterward without re-deriving paths.
    public IList<string> CheckpointPaths { get; init; } = [];
}
