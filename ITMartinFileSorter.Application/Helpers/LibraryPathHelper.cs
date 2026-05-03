using Microsoft.Extensions.Configuration;

namespace ITMartinFileSorter.Application.Helpers;

public static class LibraryPathHelper
{
    public static string GetLibraryPath(IConfiguration config)
    {
        var path = config["MediaSettings:LibraryRoot"];

        if (string.IsNullOrWhiteSpace(path))
            throw new Exception("LibraryRoot not configured");

        return Path.GetFullPath(path);
    }
}