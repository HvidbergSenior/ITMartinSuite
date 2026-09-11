using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Infrastructure.FileSystem;
using ITMartin.Media.Infrastructure.Persistence;
using ITMartin.Media.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Infrastructure.Pipelines.FaceIndex;

// "Every photo with the dog in it" as a folder. The face index knows only
// human faces; this runs the local object detector over the library once,
// remembers what it saw per photo, and turns a label into a SmartFolders set
// of hardlinks - exactly how the person folders are built, so the gallery
// shows "Fie" next to the people. Free to run, free to re-run: nothing here
// calls an API, and a photo is only ever looked at once.
public sealed class ObjectIndexService : IObjectIndexService
{
    // Same folders the face index skips, for the same reasons: generated,
    // internal, or content that is already indexed under its real path.
    private static readonly HashSet<string> SkippedFolders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "@eadir", "#recycle", "#snapshot", ".@__thumb", "@recently-snapshot", ".synophoto",
            ".package1", ".package2", ".package3", "thumbnails", "working", "enhanced", "manifests", "temp",
            "smartfolders", "livephotos", "_galleri", "dubletter",
        };

    private const string NoObjects = "";

    private readonly IDbContextFactory<MediaDbContext> _dbFactory;
    private readonly Func<IObjectDetectionService> _detectorFactory;
    private readonly ILogger<ObjectIndexService> _logger;

    public ObjectIndexService(
        IDbContextFactory<MediaDbContext> dbFactory,
        Func<IObjectDetectionService> detectorFactory,
        ILogger<ObjectIndexService> logger)
    {
        _dbFactory = dbFactory;
        _detectorFactory = detectorFactory;
        _logger = logger;
    }

    public async Task<int> IndexAsync(string libraryPath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(libraryPath)) return 0;

        HashSet<string> done;
        await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
        {
            done = new HashSet<string>(
                await db.MediaObjectTags.Select(x => x.RelativePath).Distinct().ToListAsync(cancellationToken),
                StringComparer.OrdinalIgnoreCase);
        }

        var files = EnumerateImages(libraryPath)
            .Where(f => !done.Contains(RelativeKey(libraryPath, f)))
            .ToList();

        _logger.LogInformation("Object index for {Path}: {Todo} images to scan ({Done} already done)", libraryPath, files.Count, done.Count);
        if (files.Count == 0) return 0;

        // One detector per worker, exactly like the face index - the ONNX
        // session serialises calls, so parallelism has to come from
        // independent instances.
        var workers = Math.Min(8, Math.Max(1, Environment.ProcessorCount - 2));
        var pool = new System.Collections.Concurrent.ConcurrentBag<IObjectDetectionService>();
        for (var i = 0; i < workers; i++) pool.Add(_detectorFactory());

        var pending = new System.Collections.Concurrent.ConcurrentBag<MediaObjectTagEntity>();
        var scanned = 0;
        var saveLock = new SemaphoreSlim(1, 1);

        async Task FlushAsync()
        {
            await saveLock.WaitAsync(cancellationToken);
            try
            {
                var batch = new List<MediaObjectTagEntity>();
                while (pending.TryTake(out var e)) batch.Add(e);
                if (batch.Count == 0) return;
                await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
                db.MediaObjectTags.AddRange(batch);
                await db.SaveChangesAsync(cancellationToken);
            }
            finally { saveLock.Release(); }
        }

        try
        {
            await Parallel.ForEachAsync(files, new ParallelOptions { MaxDegreeOfParallelism = workers, CancellationToken = cancellationToken }, async (file, ct) =>
            {
                if (!pool.TryTake(out var detector)) detector = _detectorFactory();
                try
                {
                    var found = await detector.DetectAsync(file, 0.30, ct);
                    var rel = RelativeKey(libraryPath, file);
                    var now = DateTimeOffset.UtcNow;

                    if (found.Count == 0)
                        pending.Add(new MediaObjectTagEntity { Id = Guid.NewGuid(), MediaFilePath = file, RelativePath = rel, Label = NoObjects, Confidence = 0, CreatedAtUtc = now });
                    foreach (var d in found)
                        pending.Add(new MediaObjectTagEntity { Id = Guid.NewGuid(), MediaFilePath = file, RelativePath = rel, Label = d.Label, Confidence = d.Confidence, CreatedAtUtc = now });

                    var n = Interlocked.Increment(ref scanned);
                    if (n % 500 == 0)
                    {
                        _logger.LogInformation("Object index progress: {Done}/{Total}", n, files.Count);
                        await FlushAsync();
                    }
                }
                finally { pool.Add(detector); }
            });
        }
        finally
        {
            await FlushAsync();
            foreach (var d in pool) (d as IDisposable)?.Dispose();
        }

        _logger.LogInformation("Object index for {Path}: scanned {Scanned} images", libraryPath, scanned);
        return scanned;
    }

    public async Task<(int Scanned, int WithLabel)> StatusAsync(string libraryPath, string label)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var scanned = await db.MediaObjectTags.Select(x => x.RelativePath).Distinct().CountAsync();
        var with = await db.MediaObjectTags.Where(x => x.Label == label).Select(x => x.RelativePath).Distinct().CountAsync();
        return (scanned, with);
    }

    public async Task<int> GenerateFolderAsync(string libraryPath, string label, string folderName, double minConfidence = 0.4, CancellationToken cancellationToken = default)
    {
        List<string> files;
        await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
        {
            files = await db.MediaObjectTags
                .Where(x => x.Label == label && x.Confidence >= minConfidence)
                .Select(x => x.MediaFilePath)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(cancellationToken);
        }

        var folder = Path.Combine(libraryPath, "SmartFolders", "People", folderName);
        Directory.CreateDirectory(folder);
        foreach (var existing in Directory.EnumerateFiles(folder))
        {
            try { File.Delete(existing); } catch { /* best effort */ }
        }

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var linked = 0;
        foreach (var source in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(source)) continue;

            var name = Path.GetFileName(source);
            var finalName = name;
            var i = 1;
            while (!used.Add(finalName))
                finalName = $"{Path.GetFileNameWithoutExtension(name)}_{i++}{Path.GetExtension(name)}";

            var dest = Path.Combine(folder, finalName);
            if (!HardLink.TryCreate(source, dest))
            {
                try { File.Copy(source, dest, overwrite: true); }
                catch { continue; }
            }
            linked++;
        }

        _logger.LogInformation("Generated object folder {Folder} for '{Label}' >= {Min}: {Linked}/{Total} files", folderName, label, minConfidence, linked, files.Count);
        return linked;
    }

    private static IEnumerable<string> EnumerateImages(string root)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            IEnumerable<string> subs;
            IEnumerable<string> files;
            try
            {
                subs = Directory.EnumerateDirectories(dir);
                files = Directory.EnumerateFiles(dir);
            }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }

            foreach (var f in files)
                if (Media.MediaTypeHelper.IsImage(f)) yield return f;

            foreach (var s in subs)
            {
                var name = Path.GetFileName(s);
                if (name.StartsWith('.') || name.StartsWith('@') || name.StartsWith('#')) continue;
                if (SkippedFolders.Contains(name)) continue;
                stack.Push(s);
            }
        }
    }

    private static string RelativeKey(string root, string file) =>
        Path.GetRelativePath(root, file).Replace('\\', '/');
}
