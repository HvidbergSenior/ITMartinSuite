using System.Text;
using ITMartin.Media.Contracts.Configuration;

namespace ITMartin.Media.Infrastructure.Media;

public sealed class Hi8PipelineRunner
{
    public string BuildVideoFilters(
        Hi8PipelineOptions options)
    {
        var filters = new List<string>();

        if (options.Video.Deinterlace.Enabled)
        {
            filters.Add(
                $"{options.Video.Deinterlace.Method}=mode={options.Video.Deinterlace.Mode}");
        }

        if (options.Video.Crop.Enabled)
        {
            filters.Add(
                BuildCropFilter(options.Video.Crop));
        }

        if (options.Video.Denoise.Enabled)
        {
            filters.Add(
                BuildDenoiseFilter(options.Video.Denoise));
        }

        if (options.Video.ColorCorrection.Enabled)
        {
            filters.Add(
                BuildColorCorrectionFilter(
                    options.Video.ColorCorrection));
        }

        return string.Join(",", filters);
    }

    public string BuildAudioFilters(
        Hi8PipelineOptions options)
    {
        var filters = new List<string>();

        if (options.Audio.Normalize.Enabled)
        {
            filters.Add(
                $"loudnorm=I={options.Audio.Normalize.TargetLufs}");
        }

        if (options.Audio.HumRemoval.Enabled)
        {
            filters.Add(
                $"afftdn=nr=20:nf=-25");
        }

        return string.Join(",", filters);
    }

    public string BuildFfmpegArguments(
        Hi8PipelineOptions options)
    {
        var videoFilters =
            BuildVideoFilters(options);

        var audioFilters =
            BuildAudioFilters(options);

        var args = new StringBuilder();

        args.Append($"-i \"{options.Input.Source}\" ");

        if (!string.IsNullOrWhiteSpace(videoFilters))
        {
            args.Append($"-vf \"{videoFilters}\" ");
        }

        if (!string.IsNullOrWhiteSpace(audioFilters))
        {
            args.Append($"-af \"{audioFilters}\" ");
        }

        args.Append($"-c:v {options.Output.VideoCodec} ");
        args.Append($"-preset {options.Output.Preset} ");
        args.Append($"-crf {options.Output.Crf} ");

        args.Append($"-c:a {options.Output.AudioCodec} ");

        args.Append($"\"{options.Output.FileNaming.Cleaned}\"");

        return args.ToString();
    }

    private static string BuildCropFilter(
        CropOptions crop)
    {
        return
            $"crop=in_w-{crop.Left + crop.Right}:in_h-{crop.Top + crop.Bottom}:{crop.Left}:{crop.Top}";
    }

    private static string BuildDenoiseFilter(
        DenoiseOptions denoise)
    {
        return denoise.Strength switch
        {
            "light" => "hqdn3d=1.5:1.5:6:6",
            "medium" => "hqdn3d=3:3:6:6",
            "strong" => "hqdn3d=6:6:12:12",
            _ => "hqdn3d"
        };
    }

    private static string BuildColorCorrectionFilter(
        ColorCorrectionOptions color)
    {
        return
            $"eq=brightness={color.Brightness}:contrast={color.Contrast}:saturation={color.Saturation}";
    }
}