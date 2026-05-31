using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Helpers;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests.Package2;

namespace ITMartin.Media.Application.Pipelines.Package2.Services;

public sealed class Package2WorkflowFactory
{
    public Package2WorkflowState Create(
        Package1Manifest manifest,
        StartPackage2Request request)
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
                            x.NormalizedPath ??
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
                        Segments =
                            x.Segments
                                .Select(segment =>
                                    new MediaSegment
                                    {
                                        Start =
                                            segment.Start,

                                        End =
                                            segment.End
                                    })
                                .ToList(),
                    })
                .ToList();

        return new Package2WorkflowState
        {
            ManualSegments =
[
    new()
    {
        Name = "Jul_Mogens_Jonna",
        Start = new TimeSpan(0, 0, 0),
        End = new TimeSpan(0, 1, 50)
    },
    new()
    {
        Name = "Jesper_Julemand",
        Start = new TimeSpan(0, 12, 32),
        End = new TimeSpan(0, 16, 38)
    },
    new()
    {
        Name = "Morgenhygge_SDR_Vium",
        Start = new TimeSpan(0, 16, 38),
        End = new TimeSpan(0, 20, 19)
    },
    new()
    {
        Name = "Strudse",
        Start = new TimeSpan(0, 20, 19),
        End = new TimeSpan(0, 21, 06)
    },
    new()
    {
        Name = "Udenfor_I_Sne",
        Start = new TimeSpan(0, 21, 06),
        End = new TimeSpan(0, 23, 41)
    },
    new()
    {
        Name = "Fastelavn_Bhv",
        Start = new TimeSpan(0, 23, 41),
        End = new TimeSpan(0, 39, 04)
    },
    new()
    {
        Name = "Fodselsdag_Mads",
        Start = new TimeSpan(0, 39, 04),
        End = new TimeSpan(0, 40, 02)
    },
    new()
    {
        Name = "Jesper_Erik_Bad",
        Start = new TimeSpan(0, 40, 02),
        End = new TimeSpan(0, 41, 36)
    },
    new()
    {
        Name = "Lille_Erik",
        Start = new TimeSpan(0, 41, 36),
        End = new TimeSpan(0, 42, 10)
    },
    new()
    {
        Name = "Rulleskojter",
        Start = new TimeSpan(0, 42, 10),
        End = new TimeSpan(0, 44, 00)
    },
    new()
    {
        Name = "Bornegymnastik",
        Start = new TimeSpan(0, 44, 00),
        End = new TimeSpan(0, 52, 34)
    },
    new()
    {
        Name = "Skovtur",
        Start = new TimeSpan(0, 52, 34),
        End = new TimeSpan(1, 05, 05)
    },
    new()
    {
        Name = "SdrVium",
        Start = new TimeSpan(1, 05, 05),
        End = new TimeSpan(1, 15, 00)
    },
    new()
    {
        Name = "Strand",
        Start = new TimeSpan(1, 15, 00),
        End = new TimeSpan(1, 25, 32)
    },
    new()
    {
        Name = "Leg",
        Start = new TimeSpan(1, 25, 32),
        End = new TimeSpan(1, 26, 28)
    },
    new()
    {
        Name = "Mads_Erik_Bad",
        Start = new TimeSpan(1, 26, 28),
        End = new TimeSpan(1, 32, 25)
    },
    new()
    {
        Name = "Ferie",
        Start = new TimeSpan(1, 32, 25),
        End = new TimeSpan(1, 40, 00)
    }
            ], 
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

    private static Package2Configuration BuildConfiguration(
        RestorationProfile restorationProfile,
        EnhancementProfile enhancementProfile)
    {
        var configuration =
            new Package2Configuration
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