using Microsoft.Extensions.Logging;

namespace ITMartinForloebet.Infrastructure;

/// <summary>Markdown per område under Knowledge/. Loaded once; the whole set goes
/// into the analysis prompt (it is small - a few pages per område).</summary>
public sealed class Knowledge
{
    public IReadOnlyDictionary<string, string> Files { get; }

    public Knowledge(string dir, ILogger<Knowledge> logger)
    {
        var files = new Dictionary<string, string>();
        if (Directory.Exists(dir))
        {
            foreach (var f in Directory.GetFiles(dir, "*.md"))
                files[Path.GetFileNameWithoutExtension(f)] = File.ReadAllText(f);
        }
        Files = files;
        logger.LogInformation("Knowledge: {Count} files ({Chars} chars)", files.Count, files.Values.Sum(v => v.Length));
    }

    public string All() => string.Join("\n\n---\n\n", Files.Select(kv => $"# Videnbase: {kv.Key}\n\n{kv.Value}"));
}
