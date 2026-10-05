namespace ITMartinHjem.Server.Services;

// A page's text cut at its "## " headings, so each part can be its own show/hide box (user 2026-10-05:
// "parts on the box instead of the whole" - easier to read). "### " stays inside its part (TinyMarkdown's Læs mere cards).
public static class Sections
{
    public sealed record Part(string Title, string Body);

    public static (string Intro, List<Part> Parts) Split(string? text)
    {
        var lines = (text ?? "").Replace("\r", "").Split('\n');
        var intro = new List<string>();
        var parts = new List<Part>();
        string? title = null;
        var body = new List<string>();
        foreach (var line in lines)
        {
            if (line.StartsWith("## "))
            {
                if (title is not null) parts.Add(new Part(title, string.Join('\n', body).Trim()));
                title = line[3..].Trim().Replace("**", "");
                body = [];
            }
            else if (title is null) intro.Add(line);
            else body.Add(line);
        }
        if (title is not null) parts.Add(new Part(title, string.Join('\n', body).Trim()));
        return (string.Join('\n', intro).Trim(), parts);
    }
}
