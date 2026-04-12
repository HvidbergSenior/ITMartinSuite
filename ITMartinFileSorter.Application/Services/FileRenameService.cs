using System.Text.RegularExpressions;
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
                SanitizeFileName(file.FileName),

            RenameStrategy.AlbumStyle =>
                SanitizeFileName(_albumStyleNameBuilder.Build(file, index)),

            RenameStrategy.DateAndType =>
                BuildDateTypeName(file, index),

            RenameStrategy.DateOnly =>
                BuildDateOnlyName(file, index),

            _ => SanitizeFileName(file.FileName)
        };
    }

    private string BuildDateTypeName(MediaFile file, int index)
    {
        var bestDate = _mediaDateService.GetBestDate(file.FullPath);
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        var fileName =
            $"{bestDate:yyyy-MM-dd}_{file.MainCategory.ToString().ToLower()}_{index:D3}{ext}";

        return SanitizeFileName(fileName);
    }

    private string BuildDateOnlyName(MediaFile file, int index)
    {
        var bestDate = _mediaDateService.GetBestDate(file.FullPath);
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        var fileName =
            $"{bestDate:yyyy-MM-dd_HH-mm-ss}_{index:D3}{ext}";

        return SanitizeFileName(fileName);
    }

    private string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        name = name
            .Replace(" ", "_")
            .Replace("æ", "ae")
            .Replace("ø", "oe")
            .Replace("å", "aa")
            .Replace("Æ", "Ae")
            .Replace("Ø", "Oe")
            .Replace("Å", "Aa");

        name = Regex.Replace(name, @"[^a-zA-Z0-9_\-]", "");

        return name + ext;
    }
}