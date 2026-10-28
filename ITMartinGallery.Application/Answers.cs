using ITMartinGallery.Domain;

namespace ITMartinGallery.Application;

// What a use case answers; the Server turns it into the HTTP status.
public enum Outcome { Ok, NotFound, BadRequest }

public sealed record Answer<T>(Outcome Outcome, T? Value = default, string? Message = null)
{
    public static Answer<T> Ok(T value) => new(Outcome.Ok, value);
    public static Answer<T> NotFound() => new(Outcome.NotFound);
    public static Answer<T> BadRequest(string? message = null) => new(Outcome.BadRequest, default, message);
}

// One picture/video/song/document tile. Null fields are part of the answer (the pages read them).
public sealed record FileTile(string Name, string RelPath, string WebPath, string? Thumb, bool IsVideo, bool IsAudio, bool IsDoc,
    string? LiveVideo, string? MonthLabel, string[]? People);

public sealed record PersonLink(string Name, string RelPath);

public sealed record BrowsePage(bool AtRoot, string? ParentRelPath, List<FolderEntry> Folders, List<FileTile> Files,
    bool HideAddons, bool ViewOnly, string? Title, string? Theme, string? Headline, string? Tagline, string? UploadUrl, PersonLink[]? People);
