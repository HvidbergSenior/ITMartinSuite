using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Helpers;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests.Package4;

namespace ITMartin.Media.Application.Pipelines.Package4.Services;

public sealed class Package4WorkflowFactory
{
    public Package4WorkflowState Create(Package1Manifest manifest, StartPackage4Request request)
    {
        var items = manifest.MediaFiles
            .Where(x => x.ExportedPath is not null && MediaTypeHelper.IsVideo(x.ExportedPath))
            .Select(x => new EnhancedMediaItem
            {
                OriginalPath = x.OriginalPath,
                NormalizedPath = x.ExportedPath!,
                CurrentWorkingPath = x.ExportedPath!,
                MediaKind = MediaKind.Video,
                Operations =
                [
                    new EnhancementOperation
                    {
                        Name = "Imported",
                        StartedAt = DateTimeOffset.UtcNow,
                        Success = true,
                        CompletedAt = DateTimeOffset.UtcNow
                    }
                ],
            })
            .ToList();

        return new Package4WorkflowState
        {
            PackageId = Guid.NewGuid(),
            WorkingDirectory = request.WorkingDirectory,
            Items = items,

            EnableWhiteBalance = request.EnableWhiteBalance,
            EnableExposureContrast = request.EnableExposureContrast,
            EnableSaturationVibrance = request.EnableSaturationVibrance,
            EnableColorGrade = request.EnableColorGrade,
            EnableSharpen = request.EnableSharpen,
            EnableNoiseReduction = request.EnableNoiseReduction,
            EnableDeflicker = request.EnableDeflicker,
            EnableStabilization = request.EnableStabilization,
            EnableStabilizedCrop = request.EnableStabilizedCrop,

            EnableAudioNoiseReduction = request.EnableAudioNoiseReduction,
            EnableWindNoiseReduction = request.EnableWindNoiseReduction,
            EnableHumRemoval = request.EnableHumRemoval,
            EnableAudioEq = request.EnableAudioEq,
            EnableDeEss = request.EnableDeEss,
            EnableAudioCompression = request.EnableAudioCompression,
            EnableLoudnessNormalization = request.EnableLoudnessNormalization,

            EnableTrim = request.EnableTrim,
            TrimStartSeconds = request.TrimStartSeconds,
            TrimEndSeconds = request.TrimEndSeconds,

            DeliveryCrf = request.DeliveryCrf,
            DeliveryMaxRateMbps = request.DeliveryMaxRateMbps,
            DeliveryAudioBitrate = request.DeliveryAudioBitrate
        };
    }
}
