using ITMartinKigMed.Domain;

namespace ITMartinKigMed.Application;

// Where the session survives a restart (session.json) and where ended sessions are archived.
public interface ISessionStore
{
    LiveSession Load();
    void Save(LiveSession session);
    void Archive(LiveSession session, string name);
}

// Pictures viewers send in with an idea. Names are random; the store only accepts its own names back.
public interface IPictureStore
{
    Task<string> SaveAsync(Stream jpeg, CancellationToken ct = default);
    string? PathOf(string name);   // null = not a picture name we made, or the file is gone
}

// The video server: is Martin's video actually arriving right now?
public interface IMediaProbe
{
    Task<bool> VideoReadyAsync(CancellationToken ct);
}
