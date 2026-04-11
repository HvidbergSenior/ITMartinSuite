using ITMartinFileSorter.Application.Helpers;
using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Enums;

namespace ITMartinFileSorter.Application.Services;

public static class AlbumStyleNameBuilder
{
    public static string Build(MediaFile file, int index)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        var date =
            ImageMetadataHelper.GetCreationTime(file.FullPath) ??
            VideoMetadataHelper.GetCreationTime(file.FullPath) ??
            file.CreatedAt;

        // Better sortable date
        string datePart = date.ToString("yyyy-MM");

        string location = GetLocation(file);

        string type = GetTypeLabel(file.SubCategory, file.MainCategory);

        var parts = new List<string>
        {
            datePart
        };

        if (!string.IsNullOrWhiteSpace(location))
            parts.Add(location);

        if (!string.IsNullOrWhiteSpace(type))
            parts.Add(type);

        parts.Add(index.ToString("D3"));

        return string.Join(" ", parts) + ext;
    }

    private static string GetLocation(MediaFile file)
    {
        var coords = GpsHelper.GetCoordinates(file.FullPath);

        if (coords == null)
            return "";

        return LocationFilter.GetLocationName(
            coords.Value.lat,
            coords.Value.lng);
    }

    private static string GetTypeLabel(
        MediaSubCategory sub,
        MediaMainCategory main)
    {
        switch (sub)
        {
            case MediaSubCategory.Screenshot:
                return "Screenshot";

            case MediaSubCategory.ScreenRecording:
                return "Screen Recording";

            case MediaSubCategory.PhonePhoto:
            case MediaSubCategory.Camera:
            case MediaSubCategory.OtherImage:
            case MediaSubCategory.UnknownImage:
                return "Photo";

            case MediaSubCategory.PhoneVideo:
            case MediaSubCategory.Movie:
            case MediaSubCategory.Clip:
            case MediaSubCategory.OtherVideo:
            case MediaSubCategory.UnknownVideo:
                return "Video";

            case MediaSubCategory.Pdf:
                return "PDF";

            case MediaSubCategory.Word:
            case MediaSubCategory.Excel:
            case MediaSubCategory.Presentation:
            case MediaSubCategory.Text:
            case MediaSubCategory.Csv:
            case MediaSubCategory.UnknownDocument:
                return "Document";

            case MediaSubCategory.Music:
            case MediaSubCategory.VoiceMemo:
            case MediaSubCategory.UnknownAudio:
                return "Audio";
        }

        return main switch
        {
            MediaMainCategory.Image => "Photo",
            MediaMainCategory.Video => "Video",
            MediaMainCategory.Document => "Document",
            MediaMainCategory.Audio => "Audio",
            _ => "File"
        };
    }

    public static string EnsureUnique(string folder, string fileName)
    {
        string fullPath = Path.Combine(folder, fileName);

        if (!File.Exists(fullPath))
            return fileName;

        string name = Path.GetFileNameWithoutExtension(fileName);
        string ext = Path.GetExtension(fileName);

        int i = 1;

        while (true)
        {
            string newName = $"{name} ({i}){ext}";
            string newPath = Path.Combine(folder, newName);

            if (!File.Exists(newPath))
                return newName;

            i++;
        }
    }
}