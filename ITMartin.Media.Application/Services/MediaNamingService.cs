using System.Text.RegularExpressions;
using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Domain.Interfaces;
using ITMartin.Media.Interfaces;

namespace ITMartin.Media.Application.Services;

public sealed class MediaNamingService
    : IMediaNamingService
{
    public string BuildFileName(
        MediaFile file)
    {
        var extension =
            Path.GetExtension(
                file.NormalizedPath ??
                file.FullPath);

        var year =
            file.Year > 0
                ? file.Year.ToString()
                : "Unknown";

        var category =
            file.AiCategory ??
            file.MainCategory.ToString();

        var subCategory =
            file.AiSubCategory;

        var parts =
            new List<string>
            {
                year,
                category
            };

        if (!string.IsNullOrWhiteSpace(
                subCategory))
        {
            parts.Add(subCategory);
        }

        // Fallback to original name

        if (parts.Count <= 2)
        {
            var original =
                Path.GetFileNameWithoutExtension(
                    file.FileName);

            parts.Add(original);
        }

        var fileName =
            string.Join(
                "_",
                parts);

        fileName =
            Sanitize(fileName);

        return $"{fileName}{extension}";
    }

    private static string Sanitize(
        string value)
    {
        value =
            Regex.Replace(
                value,
                @"[^a-zA-Z0-9_\- ]",
                "");

        value =
            Regex.Replace(
                value,
                @"\s+",
                "_");

        value =
            Regex.Replace(
                value,
                @"_+",
                "_");

        value =
            value.Trim('_');

        if (value.Length > 80)
        {
            value = value[..80];
        }

        return value;
    }
}