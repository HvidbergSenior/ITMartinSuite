using System.Text;
using ITMartinForloebet.Application;
using ITMartinForloebet.Domain;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace ITMartinForloebet.Infrastructure;

/// <summary>Owner-only evidence on the data volume. Nothing here is served by a
/// static-file route; the page streams a file only to a request carrying the edit key.</summary>
public sealed class FileDocumentStore(string root) : IDocumentFiles
{
    public string Root { get; } = root;

    public string PathFor(string storedName) => Path.Combine(Root, storedName);

    public async Task<string> SaveAsync(string slug, string fileName, Stream data, CancellationToken ct)
    {
        Directory.CreateDirectory(Root);
        var stored = $"{slug}-{Guid.NewGuid():N}{Path.GetExtension(fileName).ToLowerInvariant()}";
        await using var fs = File.Create(PathFor(stored));
        await data.CopyToAsync(fs, ct);
        return stored;
    }

    public async Task<DokumentIndhold?> ReadForAiAsync(Dokument d, CancellationToken ct)
    {
        if (d.StoredName.Length == 0) return null;
        var path = PathFor(d.StoredName);
        if (DokumentArt.IsImage(d.ContentType)) return new DokumentIndhold(null, await ShrinkForAiAsync(path, ct), "image/jpeg", null);
        if (DokumentArt.IsPdf(d.ContentType, d.FileName)) return new DokumentIndhold(null, null, null, await File.ReadAllBytesAsync(path, ct));
        if (DokumentArt.IsText(d.ContentType, d.FileName)) return new DokumentIndhold(ReadTextForAi(path), null, null, null);
        return null;
    }

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
