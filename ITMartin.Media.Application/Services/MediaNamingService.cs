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
        var extension =
            Path.GetExtension(
                file.NormalizedPath ??
                file.FullPath);

        var original =
            Path.GetFileNameWithoutExtension(
                file.FileName);

        original =
            Sanitize(original);

        return $"{original}{extension}";
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