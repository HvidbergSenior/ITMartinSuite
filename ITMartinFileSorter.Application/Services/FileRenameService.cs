using ITMartinFileSorter.Application.Helpers;
using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Interfaces;

namespace ITMartinFileSorter.Application.Services;

public class FileRenameService
{
    private readonly IMediaDateService _mediaDateService;
    private readonly AlbumStyleNameBuilder _albumStyleNameBuilder;

    public FileRenameService(
        IMediaDateService mediaDateService,
        AlbumStyleNameBuilder albumStyleNameBuilder)
    {
        _mediaDateService = mediaDateService;
        _albumStyleNameBuilder = albumStyleNameBuilder;
    }

    public string BuildName(
        MediaFile file,
        int index,
        RenameStrategy strategy)
    {
        return strategy switch
        {
            RenameStrategy.KeepOriginal =>
                file.FileName,

            RenameStrategy.AlbumStyle =>
                _albumStyleNameBuilder.Build(file, index),

            RenameStrategy.DateAndType =>
                BuildDateTypeName(file, index),

            RenameStrategy.DateOnly =>
                BuildDateOnlyName(file, index),

            _ => file.FileName
        };
    }

    private string BuildDateTypeName(MediaFile file, int index)
    {
        var bestDate = _mediaDateService.GetBestDate(file.FullPath);

        var datePrefix = bestDate?.ToString("yyyy-MM-dd") ?? "Unknown-Date";
        var ext = Path.GetExtension(file.FileName);
        return $"{datePrefix} {file.MainCategory} {index:D3}{ext}";
    }

    private string BuildDateOnlyName(MediaFile file, int index)
    {
        var bestDate = _mediaDateService.GetBestDate(file.FullPath);
        var ext = Path.GetExtension(file.FileName);

        return
            $"{bestDate:yyyy-MM-dd HH-mm-ss} {index:D3}{ext}";
    }
}