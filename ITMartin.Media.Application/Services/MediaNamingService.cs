using System.Text.RegularExpressions;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Services;

public sealed class MediaNamingService
    : IMediaNamingService
{
    public string BuildFileName(
        MediaFile file)
    {
        var year =
            file.Year > 0
                ? file.Year.ToString()
                : "Unknown";

        var category =
            !string.IsNullOrWhiteSpace(file.AiCategory)
                ? file.AiCategory
                : file.MainCategory.ToString();

        var subCategory =
            !string.IsNullOrWhiteSpace(file.AiSubCategory)
                ? file.AiSubCategory
                : file.SubCategory.ToString();

        var title =
            GetTitle(file);

        var parts = new List<string>
        {
            year,
            category
        };

        if (!string.IsNullOrWhiteSpace(subCategory))
        {
            parts.Add(subCategory);
        }

        parts.Add(title);

        var name =
            string.Join(
                "_",
                parts
                    .Where(x => !string.IsNullOrWhiteSpace(x)));

        return $"{Sanitize(name)}{file.Extension}";
    }

    private static string GetTitle(
        MediaFile file)
    {
        if (!string.IsNullOrWhiteSpace(file.Title))
        {
            return file.Title;
        }

        if (!string.IsNullOrWhiteSpace(file.DocumentTitle))
        {
            return file.DocumentTitle;
        }

        if (!string.IsNullOrWhiteSpace(file.Album))
        {
            return file.Album;
        }

        return Path.GetFileNameWithoutExtension(
            file.FileName);
    }

    private static string Sanitize(
        string value)
    {
        value =
            Regex.Replace(
                value,
                @"[^a-zA-Z0-9_\- ]",
                string.Empty);

        value =
            value.Replace(
                " ",
                "_");

        value =
            Regex.Replace(
                value,
                @"_+",
                "_");

        value =
            value.Trim('_');

        return value.Length > 150
            ? value[..150]
            : value;
    }
}