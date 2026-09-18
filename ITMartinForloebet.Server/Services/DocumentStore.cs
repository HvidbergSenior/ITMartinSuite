using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace ITMartinForloebet.Server.Services;

/// <summary>Owner-only evidence on the data volume. Nothing here is served by a
/// static-file route; the page streams a file only to a request carrying the edit key.</summary>
public sealed class DocumentStore(IConfiguration config)
{
    public string Root { get; } = config["Forloebet:DocsDir"] ?? "/app/data/docs";
    public long MaxBytes { get; } = config.GetValue("Forloebet:MaxFileBytes", 8L * 1024 * 1024);
    public int MaxPerForloeb { get; } = config.GetValue("Forloebet:MaxDocsPerForloeb", 20);
    public int MaxTrinPerForloeb { get; } = config.GetValue("Forloebet:MaxTrinPerForloeb", 40);

    public string PathFor(string storedName) => Path.Combine(Root, storedName);

    public async Task<string> SaveAsync(string slug, string fileName, Stream data, CancellationToken ct)
    {
        Directory.CreateDirectory(Root);
        var stored = $"{slug}-{Guid.NewGuid():N}{Path.GetExtension(fileName).ToLowerInvariant()}";
        await using var fs = File.Create(PathFor(stored));
        await data.CopyToAsync(fs, ct);
        return stored;
    }

    public static bool IsImage(string contentType) => contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    public static bool IsPdf(string contentType, string name) => contentType == "application/pdf" || name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
    public static bool IsText(string contentType, string name) =>
        contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
        || contentType == "message/rfc822"
        || name.EndsWith(".eml", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".md", StringComparison.OrdinalIgnoreCase);

    /// <summary>Phone photos are 4-12 MB; Claude wants ≤ ~1600px. Returns JPEG bytes.</summary>
    public static async Task<byte[]> ShrinkForAiAsync(string path, CancellationToken ct)
    {
        using var img = await Image.LoadAsync(path, ct);
        img.Mutate(x => x.AutoOrient().Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(1600, 1600) }));
        using var ms = new MemoryStream();
        await img.SaveAsync(ms, new JpegEncoder { Quality = 80 }, ct);
        return ms.ToArray();
    }

    /// <summary>.eml / .txt: strip headers we don't need and quoted-printable noise, keep the body.</summary>
    public static string ReadTextForAi(string path)
    {
        var raw = File.ReadAllText(path, Encoding.UTF8);
        if (raw.Contains("Content-Transfer-Encoding: quoted-printable", StringComparison.OrdinalIgnoreCase))
            raw = DecodeQuotedPrintable(raw);
        // Drop the bulk MIME header block, keep From/Date/Subject lines and the body.
        var lines = raw.Split('\n');
        var sb = new StringBuilder();
        var inHeaders = lines.Length > 0 && lines[0].Contains(':');
        foreach (var line in lines)
        {
            if (inHeaders)
            {
                if (line.Trim().Length == 0) { inHeaders = false; continue; }
                if (line.StartsWith("From:") || line.StartsWith("Date:") || line.StartsWith("Subject:") || line.StartsWith("To:"))
                    sb.AppendLine(line);
                continue;
            }
            if (line.StartsWith("<") && line.Contains("</")) continue; // crude html skip
            sb.AppendLine(line);
        }
        return sb.ToString();
    }

    private static string DecodeQuotedPrintable(string s)
    {
        s = s.Replace("=\r\n", "").Replace("=\n", "");
        var bytes = new List<byte>();
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == '=' && i + 2 < s.Length && Uri.IsHexDigit(s[i + 1]) && Uri.IsHexDigit(s[i + 2]))
            {
                bytes.Add(Convert.ToByte(s.Substring(i + 1, 2), 16));
                i += 2;
            }
            else bytes.AddRange(Encoding.UTF8.GetBytes(s[i].ToString()));
        }
        return Encoding.UTF8.GetString(bytes.ToArray());
    }
}
