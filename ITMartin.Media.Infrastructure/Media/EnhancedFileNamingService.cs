using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Infrastructure.Media;

public sealed class EnhancedFileNamingService
    : IEnhancedFileNamingService
{
    public string BuildFileName(
        EnhancedMediaItem item)
    {
        var originalName =
            Path.GetFileNameWithoutExtension(
                item.OriginalPath);

        originalName =
            Sanitize(
                originalName);

        var extension =
            Path.GetExtension(
                item.CurrentWorkingPath
                ?? item.NormalizedPath);

        return $"{originalName}.enhanced{extension}";
    }

    private static string Sanitize(
        string value)
    {
        foreach (var invalidChar in
                 Path.GetInvalidFileNameChars())
        {
            value =
                value.Replace(
                    invalidChar,
                    '_');
        }

        return value
            .Replace(' ', '_')
            .ToLowerInvariant();
    }
}