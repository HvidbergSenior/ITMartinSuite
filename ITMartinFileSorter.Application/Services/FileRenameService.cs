using ITMartinFileSorter.Application.Helpers;
using ITMartinFileSorter.Domain.Entities;

namespace ITMartinFileSorter.Application.Services;

public static class FileRenameService
{
    public static string BuildName(
        MediaFile file,
        int index,
        RenameStrategy strategy)
    {
        return strategy switch
        {
            RenameStrategy.KeepOriginal =>
                file.FileName,

            RenameStrategy.AlbumStyle =>
                AlbumStyleNameBuilder.Build(file, index),

            RenameStrategy.DateAndType =>
                BuildDateTypeName(file, index),

            RenameStrategy.DateOnly =>
                BuildDateOnlyName(file, index),

            _ => file.FileName
        };
    }

    private static string BuildDateTypeName(MediaFile file, int index)
    {
        var bestDate = ImageMetadataHelper.GetBestDate(file.FullPath);
        var ext = Path.GetExtension(file.FileName);

        return
            $"{bestDate:yyyy-MM-dd} {file.MainCategory} {index:D3}{ext}";
    }

    private static string BuildDateOnlyName(MediaFile file, int index)
    {
        var bestDate = ImageMetadataHelper.GetBestDate(file.FullPath);
        var ext = Path.GetExtension(file.FileName);
        return
            $"{bestDate:yyyy-MM-dd HH-mm-ss} {index:D3}{ext}";
        
    }
}