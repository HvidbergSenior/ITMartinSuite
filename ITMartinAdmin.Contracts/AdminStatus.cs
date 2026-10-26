namespace ITMartinAdmin.Contracts;

// The status call (2026-10-26, ITMartinAdmin's front page): every app with something to look after answers
// GET /api/admin/status with a few numbers. Only with the key header (Admin__StatusKey, the same in every app's magic.env).
public static class AdminStatusApi
{
    public const string Path = "/api/admin/status";
    public const string KeyHeader = "X-Admin-Key";
    public const int MinKeyLength = 16;

    /// <summary>A key shorter than MinKeyLength never opens anything (not set = closed).</summary>
    public static bool KeyOk(string? configured, string? sent) =>
        configured is { Length: >= MinKeyLength } && sent == configured;
}

/// <summary>One number: "Ordrer at pakke: 3". Attention = Martin should do something (shown first, in words). Url = the
/// page where he does it.</summary>
public sealed record StatusLine(string Label, int Count, bool Attention = false, string Url = "");

/// <summary>An app's numbers right now.</summary>
public sealed record AppStatus(string App, List<StatusLine> Lines);
