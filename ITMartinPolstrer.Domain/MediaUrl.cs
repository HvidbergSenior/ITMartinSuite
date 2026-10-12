namespace ITMartinPolstrer.Domain;

// Where a stored photo/video is served: /media/<folder>/<file>, each part URL-escaped (folders are named after the piece, æøå included).
public static class MediaUrl
{
    public static string For(string relativePath) =>
        "/media/" + string.Join('/', relativePath.Replace('\\', '/').Split('/').Select(Uri.EscapeDataString));
}
