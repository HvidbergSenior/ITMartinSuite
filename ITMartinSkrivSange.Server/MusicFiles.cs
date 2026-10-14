using ITMartinSkrivSange.Application;

namespace ITMartinSkrivSange.Server;

// Playing, downloading, uploading and deleting files in the music folder (takes and hummed sketches).
public static class MusicFiles
{
    public static void MapMusicFiles(this WebApplication app)
    {
        app.MapGet("/stream", (string path, IStudioLibrary library) =>
        {
            if (library.Resolve(path) is not { } full) return Results.NotFound();
            var audioOnlyTake = Path.GetFileNameWithoutExtension(full).StartsWith("take-", StringComparison.OrdinalIgnoreCase);
            return Results.File(full, Mime(full, audioOnlyTake), enableRangeProcessing: true);
        });

        // Forces a download instead of inline playback - to hand a sketch clip to Suno.
        app.MapGet("/download", (string path, IStudioLibrary library) =>
            library.Resolve(path) is { } full
                ? Results.File(full, Mime(full, audioOnlyTake: true), fileDownloadName: Path.GetFileName(full))
                : Results.NotFound());

        app.MapPost("/api/recording/{songKey}", async (string songKey, HttpRequest req, IStudioLibrary library) =>
        {
            // Kestrel's default 30MB body cap silently truncated longer video takes.
            var size = req.HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
            if (size is { IsReadOnly: false }) size.MaxRequestBodySize = 500 * 1024 * 1024;
            var rel = await library.SaveRecordingAsync(songKey, req.Body, req.ContentType, req.Query["section"].ToString());
            return Results.Ok(new { path = rel, filename = Path.GetFileName(rel) });
        });

        app.MapDelete("/api/recording", (string path, IStudioLibrary library) => library.Delete(path) ? Results.Ok() : Results.NotFound());

        app.MapPost("/api/sketch/{songKey}", async (string songKey, HttpRequest req, IStudioLibrary library) =>
            Results.Ok(new { path = await library.SaveSketchAsync(songKey, req.Body, req.ContentType) }));

        app.MapDelete("/api/sketch", (string path, IStudioLibrary library) => library.Delete(path) ? Results.Ok() : Results.NotFound());
    }

    private static string Mime(string file, bool audioOnlyTake) => Path.GetExtension(file).ToLowerInvariant() switch
    {
        ".mp3"  => "audio/mpeg",
        ".m4a"  => "audio/mp4",
        ".wav"  => "audio/wav",
        ".ogg"  => "audio/ogg",
        ".flac" => "audio/flac",
        ".aac"  => "audio/aac",
        ".mp4"  => "video/mp4",
        ".mov"  => "video/quicktime",
        ".webm" => audioOnlyTake ? "audio/webm" : "video/webm",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png"  => "image/png",
        ".webp" => "image/webp",
        _       => "application/octet-stream",
    };
}
