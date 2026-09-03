using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Helpers;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests.AnalogDigitize;

namespace ITMartin.Media.Application.Pipelines.AnalogDigitize.Services;

public sealed class AnalogDigitizeWorkflowFactory
{
    public AnalogDigitizeWorkflowState Create(
        QuickSortManifest manifest,
        StartAnalogDigitizeRequest request)
    {
        var configuration =
            BuildConfiguration(
                request.RestorationProfile,
                request.EnhancementProfile);

        var items =
            manifest.MediaFiles
                .Where(x =>
                    x.ExportedPath is not null)
                .Select(x =>
                    new EnhancedMediaItem
                    {
                        OriginalPath =
                            x.OriginalPath,

                        NormalizedPath =
                            x.ExportedPath!,

                        CurrentWorkingPath =
                            x.ExportedPath!,

                        MediaKind =
                            MediaTypeHelper.IsVideo(
                                x.ExportedPath)
                                ? MediaKind.Video
                                : MediaKind.Image,

                        Operations =
                        [
                            new EnhancementOperation
                            {
                                Name = "Imported",
                                StartedAt =
                                    DateTimeOffset.UtcNow,

                                Success = true,

                                CompletedAt =
                                    DateTimeOffset.UtcNow
                            }
                        ],
                        Segments = [],
                    })
                .ToList();

        return new AnalogDigitizeWorkflowState
        {
            ManualSegments = [], 
            PackageId =
                Guid.NewGuid(),

            WorkingDirectory =
                Path.Combine(
                    request.SourceLibraryPath,
                    ".package2"),

            RestorationProfile =
                request.RestorationProfile,

            EnhancementProfile =
                request.EnhancementProfile,

            Configuration =
                configuration,

            // VIDEO

            EnableDeinterlace =
                configuration.Video.EnableDeinterlace,

            EnableCrop =
                configuration.Video.EnableCrop,

            EnableDenoise =
                configuration.Video.EnableDenoise,

            EnableSharpen =
                configuration.Video.EnableSharpen,

            EnableUpscaling =
                configuration.Video.EnableUpscaling,

            EnableStabilization =
                configuration.Video.EnableStabilization,

            EnableColorCorrection =
                configuration.Video.EnableColorCorrection,
            SampleCount = 3,

            SampleDuration =
                TimeSpan.FromSeconds(20),
            // AUDIO

            EnableAudioEnhancement =
                configuration.Audio.EnableEnhancement,

            EnableAudioNormalize =
                configuration.Audio.EnableNormalize,

            EnableAudioNoiseReduction =
                configuration.Audio.EnableNoiseReduction,

            EnableHumRemoval =
                configuration.Audio.EnableHumRemoval,

            EnableAiEnhancement =
                configuration.Audio.EnableSpeechEnhancement,
            EnableSampleGeneration = false,
            Items =
                items,
            
        };
    }

    private static AnalogDigitizeConfiguration BuildConfiguration(
        RestorationProfile restorationProfile,
        EnhancementProfile enhancementProfile)
    {
        var configuration =
            new AnalogDigitizeConfiguration
            {
                RestorationProfile =
                    restorationProfile,
                EnhancementProfile = enhancementProfile
            };

        ApplyRestorationProfile(
            configuration,
            restorationProfile);

        ApplyEnhancementProfile(
            configuration,
            enhancementProfile);

        return configuration;
    }

    private static void ApplyRestorationProfile(
        AnalogDigitizeConfiguration configuration,
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

                configuration.Video.EnableCrop =
                    true;

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
        AnalogDigitizeConfiguration configuration,
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