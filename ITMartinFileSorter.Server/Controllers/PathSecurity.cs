namespace ITMartinFileSorter.Server.Controllers;

/// <summary>
/// Shared containment check for any endpoint that serves or exports files by a
/// caller-supplied path. Requires the resolved path to be exactly the root or a
/// proper descendant of it - a plain StartsWith on unseparated strings would let
/// a sibling like "C:\library\mie2" pass as "under" "C:\library\mie".
/// </summary>
internal static class PathSecurity
{
    public static bool IsUnder(string path, string root)
    {
        if (string.IsNullOrWhiteSpace(root)) return false;

        var fullPath = Path.GetFullPath(path);
        var fullRoot = Path.GetFullPath(root);

        return fullPath.Equals(fullRoot, StringComparison.OrdinalIgnoreCase)
            || fullPath.StartsWith(
                fullRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
    }
}
