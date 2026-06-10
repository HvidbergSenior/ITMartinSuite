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
        var title =
            GetTitle(file);

        var extension =
            !string.IsNullOrWhiteSpace(
                file.NormalizedPath)
                ? Path.GetExtension(
                    file.NormalizedPath)
                : file.Extension;

        return
            $"{Sanitize(title)}{extension.ToLowerInvariant()}";
    }

    private static string GetTitle(
        MediaFile file)
    {
        if (!string.IsNullOrWhiteSpace(
                file.AiDescription))
        {
            return file.AiDescription;
        }

        if (!string.IsNullOrWhiteSpace(
                file.Title))
        {
            return file.Title;
        }

        if (!string.IsNullOrWhiteSpace(
                file.DocumentTitle))
        {
            return file.DocumentTitle;
        }

        if (!string.IsNullOrWhiteSpace(
                file.Album))
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
                @"[^a-zA-Z0-9æøåÆØÅ_\- ]",
                string.Empty);

        value =
            value.Replace(
                "_",
                " ");

        value =
            Regex.Replace(
                value,
                @"\s+",
                " ");

        value =
            value.Trim();

        return value.Length > 150
            ? value[..150]
            : value;
    }
}