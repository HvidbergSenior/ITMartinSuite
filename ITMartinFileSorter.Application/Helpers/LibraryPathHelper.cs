using Microsoft.Extensions.Configuration;
namespace ITMartinFileSorter.Application.Helpers;

public static class LibraryPathHelper
{
    public static string GetLibraryPath(IConfiguration config)
    {
        var baseRoot = config["MediaSettings:LibraryRoot"]!;
        return Path.Combine(baseRoot, "Library");
    }
}