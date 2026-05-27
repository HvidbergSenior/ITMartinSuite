using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Pipelines.Package2.Services;

public sealed class Package2ProfileBuilder
{
    public Package2Configuration Build(
        RestorationProfile restoration,
        EnhancementProfile enhancement)
    {
        var configuration =
            new Package2Configuration
            {
                RestorationProfile =
                    restoration,

                EnhancementProfile =
                    enhancement
            };

        ApplyRestorationProfile(
            configuration,
            restoration);

        ApplyEnhancementProfile(
            configuration,
            enhancement);

        return configuration;
    }

    private static void ApplyRestorationProfile(
        Package2Configuration configuration,
        RestorationProfile profile)
    {
        switch (profile)
        {
            case RestorationProfile.VHSAggressive:

                configuration.Video.EnableDeinterlace =
                    true;

                configuration.Video.EnableDenoise =
                    true;

                configuration.Video.EnableSharpen =
                    true;

                configuration.Video.EnableCrop =
                    true;

                configuration.Video.EnableStabilization =
                    true;

                configuration.Video.EnableColorCorrection =
                    true;

                configuration.Audio.EnableEnhancement =
                    true;

                configuration.Audio.EnableNoiseReduction =
                    true;

                configuration.Audio.EnableNormalize =
                    true;

                configuration.Audio.EnableSpeechEnhancement =
                    true;

                break;

            case RestorationProfile.Hi8:

                configuration.Video.EnableDeinterlace =
                    true;

                configuration.Video.EnableDenoise =
                    true;

                configuration.Video.EnableSharpen =
                    false;

                configuration.Video.EnableCrop =
                    true;

                configuration.Video.EnableStabilization =
                    false;

                configuration.Video.EnableColorCorrection =
                    true;

                configuration.Audio.EnableEnhancement =
                    true;

                configuration.Audio.EnableNoiseReduction =
                    true;

                configuration.Audio.EnableNormalize =
                    true;

                break;

            case RestorationProfile.CleanDigitalCapture:

                configuration.Video.EnableDeinterlace =
                    false;

                configuration.Video.EnableDenoise =
                    false;

                configuration.Video.EnableSharpen =
                    false;

                configuration.Video.EnableCrop =
                    false;

                configuration.Video.EnableStabilization =
                    false;

                configuration.Video.EnableColorCorrection =
                    true;

                configuration.Audio.EnableNormalize =
                    true;

                break;

            case RestorationProfile.HandheldCamera:

                configuration.Video.EnableStabilization =
                    true;

                configuration.Video.EnableColorCorrection =
                    true;

                break;

            case RestorationProfile.FamilyArchive:

                configuration.Video.EnableDeinterlace =
                    true;

                configuration.Video.EnableDenoise =
                    true;

                configuration.Video.EnableColorCorrection =
                    true;

                configuration.Audio.EnableNormalize =
                    true;

                break;
        }
    }

    private static void ApplyEnhancementProfile(
        Package2Configuration configuration,
        EnhancementProfile profile)
    {
        switch (profile)
        {
            case EnhancementProfile.FastPreview:

                configuration.Video.TargetHeight =
                    720;

                configuration.Video.Crf =
                    28;

                configuration.Video.Preset =
                    "veryfast";

                break;

            case EnhancementProfile.Standard:

                configuration.Video.TargetHeight =
                    1080;

                configuration.Video.Crf =
                    23;

                configuration.Video.Preset =
                    "medium";

                break;

            case EnhancementProfile.HighQuality:

                configuration.Video.TargetHeight =
                    1080;

                configuration.Video.Crf =
                    18;

                configuration.Video.Preset =
                    "slow";

                break;

            case EnhancementProfile.Archival:

                configuration.Video.TargetHeight =
                    1440;

                configuration.Video.Crf =
                    14;

                configuration.Video.Preset =
                    "slow";

                break;

            case EnhancementProfile.WebOptimized:

                configuration.Video.TargetHeight =
                    1080;

                configuration.Video.Crf =
                    28;

                configuration.Video.Preset =
                    "fast";

                break;

            case EnhancementProfile.AggressiveAi:

                configuration.Video.TargetHeight =
                    2160;

                configuration.Video.Crf =
                    16;

                configuration.Video.Preset =
                    "slow";

                configuration.Video.EnableUpscaling =
                    true;

                break;
        }
    }
}