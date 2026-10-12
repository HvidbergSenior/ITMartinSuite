using System.Text.RegularExpressions;
using ITMartinKigMed.Application;

namespace ITMartinKigMed.Infrastructure;

// <data>/pictures/<random guid>.jpg - the name is the only key, so nothing but our own names is ever opened.
public sealed partial class FilePictureStore : IPictureStore
{
    private readonly string _dir;

    public FilePictureStore(string dataDir)
    {
        _dir = Path.Combine(dataDir, "pictures");
        Directory.CreateDirectory(_dir);
    }

    public async Task<string> SaveAsync(Stream jpeg, CancellationToken ct = default)
    {
        var name = Guid.NewGuid().ToString("N") + ".jpg";
        await using var fs = File.Create(Path.Combine(_dir, name));
        await jpeg.CopyToAsync(fs, ct);
        return name;
    }

    public string? PathOf(string name)
    {
        if (!PictureName().IsMatch(name)) return null;
        var file = Path.Combine(_dir, name);
        return File.Exists(file) ? file : null;
    }

    [GeneratedRegex("^[0-9a-f]{32}\\.jpg$")]
    private static partial Regex PictureName();
}
