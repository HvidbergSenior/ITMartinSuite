using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ITMartinHjemmeside.Application;
using ITMartinHjemmeside.Domain;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace ITMartinHjemmeside.Infrastructure;

/// <summary>
/// One folder per site under &lt;DataDir&gt;/sites/&lt;id&gt;: site.json, history/ (Fortryd) and billeder/.
/// Files are written via a temp file, so a crash or the nightly backup never sees half a file.
/// </summary>
public sealed partial class SiteFolders : ISiteStore, IPictureStore
{
    private static readonly JsonSerializerOptions Opt = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    private readonly string _root;
    private readonly object _gate = new();

    public SiteFolders(string dataDir)
    {
        _root = Path.Combine(dataDir, "sites");
        Directory.CreateDirectory(_root);
    }

    private string Dir(string id) =>
        IdShape().IsMatch(id) ? Path.Combine(_root, id) : throw new ArgumentException("Bad site id", nameof(id));

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,29}$")]
    private static partial Regex IdShape();

    [GeneratedRegex(@"^[0-9a-f]{12}\.jpg$")]
    private static partial Regex PictureShape();

    // ---- sites ----

    public IReadOnlyList<Site> All()
    {
        lock (_gate)
            return Directory.EnumerateDirectories(_root)
                .Select(d => Load(Path.Combine(d, "site.json")))
                .OfType<Site>().ToList();
    }

    public Site? Get(string id)
    {
        if (!IdShape().IsMatch(id)) return null;
        lock (_gate) return Load(Path.Combine(Dir(id), "site.json"));
    }

    public void Save(Site site)
    {
        lock (_gate)
        {
            Directory.CreateDirectory(Dir(site.Id));
            Write(Path.Combine(Dir(site.Id), "site.json"), site);
        }
    }

    public void PushHistory(Site site)
    {
        lock (_gate)
        {
            var h = Path.Combine(Dir(site.Id), "history");
            Directory.CreateDirectory(h);
            Write(Path.Combine(h, DateTime.UtcNow.Ticks.ToString("D19") + ".json"), site);
            foreach (var old in HistoryFiles(site.Id).SkipLast(SiteRules.History)) File.Delete(old);
        }
    }

    public Site? PopHistory(string id)
    {
        lock (_gate)
        {
            var newest = HistoryFiles(id).LastOrDefault();
            if (newest is null) return null;
            var site = Load(newest);
            File.Delete(newest);
            return site;
        }
    }

    public int HistoryCount(string id) => HistoryFiles(id).Count;

    private List<string> HistoryFiles(string id)
    {
        var h = Path.Combine(Dir(id), "history");
        return Directory.Exists(h) ? Directory.GetFiles(h, "*.json").Order(StringComparer.Ordinal).ToList() : [];
    }

    private static Site? Load(string file)
    {
        if (!File.Exists(file)) return null;
        try { return JsonSerializer.Deserialize<Site>(File.ReadAllText(file), Opt); } catch { return null; }
    }

    private static void Write(string file, Site site)
    {
        var tmp = file + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(site, Opt));
        File.Move(tmp, file, true);
    }

    // ---- pictures ----

    private string Pictures(string id) => Path.Combine(Dir(id), "billeder");

    public async Task<string?> SaveAsync(string siteId, Stream content, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await CopyCapped(content, buffer, SiteRules.MaxUpload, ct);
        buffer.Position = 0;
        Image image;
        try { image = await Image.LoadAsync(buffer, ct); }
        catch (Exception e) when (e is UnknownImageFormatException or InvalidImageContentException or NotSupportedException) { return null; }
        using (image)
        {
            // Upright as the phone held it, max 1600 px, no EXIF/GPS (a picture from home must not tell where home is).
            image.Mutate(x => x.AutoOrient().Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(1600, 1600) }));
            image.Metadata.ExifProfile = null;
            image.Metadata.XmpProfile = null;
            image.Metadata.IptcProfile = null;
            var dir = Pictures(siteId);
            Directory.CreateDirectory(dir);
            var name = SiteRules.NewId() + ".jpg";
            var file = Path.Combine(dir, name);
            await image.SaveAsJpegAsync(file + ".tmp", new JpegEncoder { Quality = 82 }, ct);
            File.Move(file + ".tmp", file, true);
            return name;
        }
    }

    private static async Task CopyCapped(Stream from, Stream to, long max, CancellationToken ct)
    {
        var buf = new byte[81920];
        long total = 0;
        int n;
        while ((n = await from.ReadAsync(buf, ct)) > 0)
        {
            total += n;
            if (total > max) throw new SiteException("Billedet er for stort (højst 25 MB).");
            await to.WriteAsync(buf.AsMemory(0, n), ct);
        }
    }

    public Stream? Open(string siteId, string storedName)
    {
        if (!IdShape().IsMatch(siteId) || !PictureShape().IsMatch(storedName)) return null;
        var f = Path.Combine(Pictures(siteId), storedName);
        return File.Exists(f) ? File.OpenRead(f) : null;
    }

    public long UsedBytes(string siteId)
    {
        var dir = Pictures(siteId);
        return Directory.Exists(dir) ? Directory.EnumerateFiles(dir).Sum(f => new FileInfo(f).Length) : 0;
    }

    public void DeleteUnused(string siteId, IReadOnlySet<string> keep)
    {
        var dir = Pictures(siteId);
        if (!Directory.Exists(dir)) return;
        // A picture an older version (Fortryd) still points to stays too.
        var used = new HashSet<string>(keep);
        foreach (var h in HistoryFiles(siteId))
            if (Load(h) is { } old) used.UnionWith(old.Pictures());
        foreach (var f in Directory.EnumerateFiles(dir, "*.jpg"))
            if (!used.Contains(Path.GetFileName(f))) File.Delete(f);
    }

    public async Task WriteZipAsync(string siteId, Stream output, CancellationToken ct)
    {
        var dir = Dir(siteId);
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(dir, f).Replace('\\', '/');
            if (rel.StartsWith("history/") || rel.EndsWith(".tmp")) continue;
            var entry = zip.CreateEntry(rel, CompressionLevel.Fastest);
            await using var to = entry.Open();
            await using var from = File.OpenRead(f);
            await from.CopyToAsync(to, ct);
        }
    }
}

public static class DependencyInjection
{
    /// <summary>The site folders (Hjemmeside:DataDir, default /data).</summary>
    public static IServiceCollection AddHjemmesideInfrastructure(this IServiceCollection services, string dataDir)
    {
        var folders = new SiteFolders(dataDir);
        services.AddSingleton<ISiteStore>(folders);
        services.AddSingleton<IPictureStore>(folders);
        return services;
    }
}
