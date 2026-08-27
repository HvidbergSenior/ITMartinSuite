namespace ITMartinStarRealms.Server.Services;

// Custom recorded sounds a player can use instead of the built-in
// synthesized tones (see the "starrealms" audio module in starrealms.js).
// One sound per trigger per profile, stored as a plain file under wwwroot -
// no DB row needed, the file's existence on disk IS the state. Uploading a
// new clip for the same trigger just overwrites the old file.
//
// The extension is NOT hardcoded to .webm - Safari/iOS's MediaRecorder
// records audio/mp4 (AAC), not webm, and saving that under a ".webm" name
// meant the browser couldn't decode it back on playback (wrong container,
// wrong Content-Type). The real extension the browser recorded in is kept
// so it plays back on the device that recorded it.
public sealed class SoundService(IWebHostEnvironment env)
{
    public static readonly string[] Triggers = ["gain", "damage", "win"];

    // Whitelist of containers real browsers' MediaRecorder actually produces -
    // never trust an arbitrary client-supplied extension onto the filesystem.
    private static readonly string[] AllowedExtensions = ["webm", "mp4", "m4a", "ogg", "wav", "mp3", "aac"];

    private string Dir(Guid profileId) => Path.Combine(env.WebRootPath, "sounds", profileId.ToString());

    public static string SanitizeExtension(string? ext)
    {
        var cleaned = (ext ?? "").Trim().TrimStart('.').ToLowerInvariant();
        return AllowedExtensions.Contains(cleaned) ? cleaned : "webm";
    }

    public async Task<string> SaveAsync(Guid profileId, string trigger, byte[] bytes, string extension)
    {
        if (!Triggers.Contains(trigger)) throw new InvalidOperationException("Ukendt lydtype");
        if (bytes.Length == 0) throw new InvalidOperationException("Optagelsen er tom");

        var ext = SanitizeExtension(extension);
        var dir = Dir(profileId);
        Directory.CreateDirectory(dir);

        // Clear out any previous recording for this trigger first, in case an
        // earlier take used a different extension (e.g. re-recording on a
        // different device/browser) - only one file per trigger should exist.
        Delete(profileId, trigger);

        var path = Path.Combine(dir, $"{trigger}.{ext}");
        await File.WriteAllBytesAsync(path, bytes);
        return $"/sounds/{profileId}/{trigger}.{ext}";
    }

    public void Delete(Guid profileId, string trigger)
    {
        var dir = Dir(profileId);
        if (!Directory.Exists(dir)) return;
        foreach (var ext in AllowedExtensions)
        {
            var path = Path.Combine(dir, $"{trigger}.{ext}");
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // trigger -> app-relative URL, for every custom sound this profile has saved.
    public Dictionary<string, string> GetMine(Guid profileId)
    {
        var dir = Dir(profileId);
        var result = new Dictionary<string, string>();
        if (!Directory.Exists(dir)) return result;

        foreach (var trigger in Triggers)
        {
            foreach (var ext in AllowedExtensions)
            {
                var path = Path.Combine(dir, $"{trigger}.{ext}");
                if (File.Exists(path)) { result[trigger] = $"/sounds/{profileId}/{trigger}.{ext}"; break; }
            }
        }
        return result;
    }
}
