namespace ITMartin.Media.Contracts.Configuration;

public sealed class Hi8PipelineOptions
{
    public PipelineOptions Pipeline { get; set; } = new();

    public InputOptions Input { get; set; } = new();

    public VideoOptions Video { get; set; } = new();

    public AudioOptions Audio { get; set; } = new();

    public OutputOptions Output { get; set; } = new();
}

public sealed class PipelineOptions
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool Enabled { get; set; }
}

public sealed class InputOptions
{
    public string Source { get; set; } = string.Empty;

    public string Type { get; set; } = "hi8";

    public string Container { get; set; } = "mp4";

    public bool PreserveOriginal { get; set; } = true;
}

public sealed class VideoOptions
{
    public bool Enabled { get; set; } = true;

    public DeinterlaceOptions Deinterlace { get; set; } = new();

    public CropOptions Crop { get; set; } = new();

    public StabilizationOptions Stabilization { get; set; } = new();

    public DenoiseOptions Denoise { get; set; } = new();

    public ColorCorrectionOptions ColorCorrection { get; set; } = new();

    public SharpenOptions Sharpen { get; set; } = new();

    public UpscaleOptions Upscale { get; set; } = new();
}

public sealed class DeinterlaceOptions
{
    public bool Enabled { get; set; }

    public string Method { get; set; } = "bwdif";

    public string Mode { get; set; } = "send_frame";
}

public sealed class CropOptions
{
    public bool Enabled { get; set; }

    public int Bottom { get; set; }

    public int Top { get; set; }

    public int Left { get; set; }

    public int Right { get; set; }
}

public sealed class StabilizationOptions
{
    public bool Enabled { get; set; }

    public string Method { get; set; } = "vidstab";

    public int Shakiness { get; set; } = 4;

    public int Accuracy { get; set; } = 8;
}

public sealed class DenoiseOptions
{
    public bool Enabled { get; set; }

    public string Method { get; set; } = "hqdn3d";

    public string Strength { get; set; } = "light";
}

public sealed class ColorCorrectionOptions
{
    public bool Enabled { get; set; }

    public double Brightness { get; set; }

    public double Contrast { get; set; } = 1.0;

    public double Saturation { get; set; } = 1.0;
}

public sealed class SharpenOptions
{
    public bool Enabled { get; set; }
}

public sealed class UpscaleOptions
{
    public bool Enabled { get; set; }
}

public sealed class AudioOptions
{
    public bool Enabled { get; set; } = true;

    public NormalizeOptions Normalize { get; set; } = new();

    public HumRemovalOptions HumRemoval { get; set; } = new();

    public NoiseReductionOptions NoiseReduction { get; set; } = new();
}

public sealed class NormalizeOptions
{
    public bool Enabled { get; set; }

    public int TargetLufs { get; set; } = -16;
}

public sealed class HumRemovalOptions
{
    public bool Enabled { get; set; }

    public int Frequency { get; set; } = 50;
}

public sealed class NoiseReductionOptions
{
    public bool Enabled { get; set; }
}

public sealed class OutputOptions
{
    public string Container { get; set; } = "mp4";

    public string VideoCodec { get; set; } = "libx264";

    public string AudioCodec { get; set; } = "aac";

    public int Crf { get; set; } = 18;

    public string Preset { get; set; } = "medium";

    public bool GeneratePreview { get; set; } = true;

    public int PreviewDurationSeconds { get; set; } = 30;

    public bool SaveIntermediateFiles { get; set; } = true;

    public FileNamingOptions FileNaming { get; set; } = new();
}

public sealed class FileNamingOptions
{
    public string Cleaned { get; set; } = "capture.cleaned.mp4";

    public string Preview { get; set; } = "capture.preview.mp4";
}