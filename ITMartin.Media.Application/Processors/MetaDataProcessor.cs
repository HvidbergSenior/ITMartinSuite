using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Enums;
using ITMartin.Media.Interfaces;

namespace ITMartin.Media.Application.Processors;

public class MetadataProcessor
{
    private readonly IMediaDateService
        _mediaDateService;

    private readonly IExifService
        _exifService;

    public MetadataProcessor(
        IMediaDateService mediaDateService,
        IExifService exifService)
    {
        _mediaDateService =
            mediaDateService;

        _exifService =
            exifService;
    }

    public void Process(
        MediaFile file,
        bool loadExif = false)
    {
        try
        {
            var result =
                _mediaDateService
                    .GetBestDate(
                        file.FullPath);

            var finalDate =
                result.date;

            if (finalDate != null)
            {
                file.SetDate(
                    finalDate,
                    result.isReliable);
            }
        }
        catch
        {
        }

        // ====================================
        // OPTIONAL EXIF
        // ====================================

        if (!loadExif)
        {
            return;
        }

        if (file.Type != MediaType.Image)
        {
            return;
        }

        try
        {
            var (width, height) =
                _exifService
                    .GetDimensions(
                        file.FullPath);

            file.Width =
                width;

            file.Height =
                height;

            file.HasExif =
                width != null &&
                height != null;
        }
        catch
        {
        }
    }
    public Task ProcessAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.CompletedTask;
    }
}