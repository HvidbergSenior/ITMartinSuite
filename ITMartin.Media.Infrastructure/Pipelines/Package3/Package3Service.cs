using System.Text.Json;
using ITMartin.Media.Contracts.Contracts.Runtime.Helpers;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Entities;
using ITMartin.Media.Infrastructure.Persistence;
using ITMartin.Media.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Infrastructure.Pipelines.Package3;

public sealed class Package3Service : IPackage3Service
{
    private static readonly HashSet<string> SkippedFolders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "@eadir", "#recycle", "#snapshot", ".@__thumb",
            "@recently-snapshot", ".synophoto", ".package1", ".package2", ".package3",
            "thumbnails", "working", "enhanced", "manifests", "temp", "smartfolders",
            // LivePhotos holds only the motion-clip companions to already-indexed
            // stills (same moment, not standalone content) - indexing/enumerating
            // them separately would double-count and, for video, means an ffprobe
            // subprocess per clip for no benefit.
            "livephotos",
            // Generated offline-gallery thumbnails - not real content, and
            // re-indexing them would duplicate faces already found in the originals.
            "_galleri"
        };

    private readonly IDbContextFactory<MediaDbContext> _dbFactory;
    private readonly Func<IFaceRecognitionService> _faceRecognitionFactory;
    private readonly IThumbnailService _thumbnailService;
    private readonly ICollectionStore _collectionStore;
    private readonly ILogger<Package3Service> _logger;

    public Package3Service(
        IDbContextFactory<MediaDbContext> dbFactory,
        Func<IFaceRecognitionService> faceRecognitionFactory,
        IThumbnailService thumbnailService,
        ICollectionStore collectionStore,
        ILogger<Package3Service> logger)
    {
        _dbFactory = dbFactory;
        _faceRecognitionFactory = faceRecognitionFactory;
        _thumbnailService = thumbnailService;
        _collectionStore = collectionStore;
        _logger = logger;
    }

    public async Task IndexFacesAsync(string libraryPath, CancellationToken cancellationToken = default)
    {
        var files = EnumerateLibraryImages(libraryPath).ToList();
        var typeName = Package3IndexType.Faces.ToString();

        await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var existing = await db.Package3IndexStatuses
                .FirstOrDefaultAsync(x => x.LibraryPath == libraryPath && x.IndexType == typeName, cancellationToken);

            if (existing is null)
            {
                existing = new Package3IndexStatusEntity
                {
                    Id = Guid.NewGuid(),
                    LibraryPath = libraryPath,
                    IndexType = typeName,
                    Status = "Running",
                    TotalFiles = files.Count,
                    ProcessedFiles = 0,
                    StartedAtUtc = DateTimeOffset.UtcNow
                };
                db.Package3IndexStatuses.Add(existing);
            }
            else
            {
                existing.Status = "Running";
                existing.TotalFiles = files.Count;
                existing.ErrorMessage = null;
                existing.CompletedAtUtc = null;
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        var processed = 0;

        // CPU-bound face extraction was running one file at a time, leaving
        // every core but one idle. FaceOnnxRecognitionService serializes all
        // calls behind its own internal lock, so real parallelism needs one
        // independent recognizer per concurrent worker, not a shared instance.
        // WAL mode + a 5s busy_timeout (SqliteWalInterceptor) make concurrent
        // SQLite writers safe; status writes are still throttled to keep
        // contention low.
        // Each worker loads its own copy of 3 ONNX models (detector, landmarks,
        // embedder) - going to ProcessorCount-1 (11 here) OOM-killed the whole
        // container. Capped at 4 concurrent recognizers as a safer starting
        // point; revisit upward only while watching real memory usage.
        var degreeOfParallelism = Math.Min(2, Math.Max(1, Environment.ProcessorCount - 1));
        var recognizerPool = new System.Collections.Concurrent.ConcurrentBag<IFaceRecognitionService>();
        for (var i = 0; i < degreeOfParallelism; i++)
            recognizerPool.Add(_faceRecognitionFactory());

        try
        {
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = degreeOfParallelism,
                CancellationToken = cancellationToken,
            };

            await Parallel.ForEachAsync(files, parallelOptions, async (file, ct) =>
            {
                if (!recognizerPool.TryTake(out var recognizer))
                    recognizer = _faceRecognitionFactory(); // pool momentarily empty - safe to just make an extra one

                try
                {
                    await using var db = await _dbFactory.CreateDbContextAsync(ct);

                    await IndexFaceForFileAsync(db, file, recognizer, ct);

                    var count = Interlocked.Increment(ref processed);

                    // Every file still gets its own index row saved (above);
                    // the shared progress counter is only written periodically
                    // (and always on the very last file) so dozens of concurrent
                    // workers aren't all fighting to update the same status row.
                    if (count % 10 == 0 || count == files.Count)
                    {
                        var status = await db.Package3IndexStatuses.FirstAsync(x => x.LibraryPath == libraryPath && x.IndexType == typeName, ct);
                        status.ProcessedFiles = count;
                        status.CurrentFile = file;
                    }

                    await db.SaveChangesAsync(ct);
                }
                finally
                {
                    recognizerPool.Add(recognizer);
                }
            });

            await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
            {
                var status = await db.Package3IndexStatuses.FirstAsync(x => x.LibraryPath == libraryPath && x.IndexType == typeName, cancellationToken);
                status.Status = "Completed";
                status.ProcessedFiles = processed;
                status.CompletedAtUtc = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Package3 face indexing failed for {LibraryPath}", libraryPath);

            await using var db = await _dbFactory.CreateDbContextAsync(CancellationToken.None);
            var status = await db.Package3IndexStatuses.FirstAsync(x => x.LibraryPath == libraryPath && x.IndexType == typeName, CancellationToken.None);
            status.Status = "Failed";
            status.ErrorMessage = ex.Message;
            await db.SaveChangesAsync(CancellationToken.None);
        }
        finally
        {
            foreach (var recognizer in recognizerPool)
                (recognizer as IDisposable)?.Dispose();
        }
    }

    private async Task IndexFaceForFileAsync(MediaDbContext db, string file, IFaceRecognitionService recognizer, CancellationToken cancellationToken)
    {
        var alreadyFaceScanned = await db.MediaFaces.AnyAsync(x => x.MediaFilePath == file, cancellationToken);
        if (alreadyFaceScanned) return;

        // FaceONNX only understands stills - for a video, pull one representative
        // frame (reusing the same ffmpeg-backed thumbnail pipeline Gallery uses)
        // and run face detection on that, but always record the match against the
        // original video path so person-folder generation links back to the video.
        var isVideo = MediaTypeHelper.IsVideo(file);
        string? tempFrame = null;
        var framePath = file;

        if (isVideo)
        {
            tempFrame = Path.Combine(Path.GetTempPath(), $"p3-frame-{Guid.NewGuid():N}.jpg");
            try
            {
                await _thumbnailService.GenerateAsync(file, tempFrame, cancellationToken);
                framePath = tempFrame;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not extract a frame from {File} for face indexing", file);
                db.MediaFaces.Add(new MediaFaceEntity
                {
                    Id = Guid.NewGuid(),
                    MediaFilePath = file,
                    EmbeddingJson = "[]",
                    CreatedAtUtc = DateTimeOffset.UtcNow
                });
                return;
            }
        }

        try
        {
            var embeddings = await recognizer.ExtractFaceEmbeddingsAsync(framePath);
            foreach (var embedding in embeddings)
            {
                db.MediaFaces.Add(new MediaFaceEntity
                {
                    Id = Guid.NewGuid(),
                    MediaFilePath = file,
                    EmbeddingJson = JsonSerializer.Serialize(embedding),
                    CreatedAtUtc = DateTimeOffset.UtcNow
                });
            }

            if (embeddings.Count == 0)
            {
                // Marker row (null embedding-less isn't possible with a required
                // column) - use an empty vector so "already scanned, no face found"
                // is distinguishable from "not yet scanned" on the next run.
                db.MediaFaces.Add(new MediaFaceEntity
                {
                    Id = Guid.NewGuid(),
                    MediaFilePath = file,
                    EmbeddingJson = "[]",
                    CreatedAtUtc = DateTimeOffset.UtcNow
                });
            }
        }
        finally
        {
            if (tempFrame is not null && File.Exists(tempFrame))
            {
                try { File.Delete(tempFrame); }
                catch { /* best effort */ }
            }
        }
    }

    public async Task<Package3IndexStatus?> GetIndexStatusAsync(string libraryPath, Package3IndexType indexType)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var typeName = indexType.ToString();
        var status = await db.Package3IndexStatuses.FirstOrDefaultAsync(x => x.LibraryPath == libraryPath && x.IndexType == typeName);

        return status is null
            ? null
            : new Package3IndexStatus
            {
                Status = status.Status,
                TotalFiles = status.TotalFiles,
                ProcessedFiles = status.ProcessedFiles,
                CurrentFile = status.CurrentFile,
                ErrorMessage = status.ErrorMessage
            };
    }

    public async Task<List<PersonDto>> GetPeopleAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.People
            .Select(p => new PersonDto
            {
                Id = p.Id,
                Name = p.Name,
                ReferencePhotoCount = db.PersonReferencePhotos.Count(r => r.PersonId == p.Id)
            })
            .ToListAsync();
    }

    public async Task<Guid> AddPersonAsync(string name, IReadOnlyList<ReferencePhotoInput> referencePhotos, string libraryPath)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var person = new PersonEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        db.People.Add(person);

        await SaveReferencePhotosAsync(db, person.Id, referencePhotos, libraryPath);
        await db.SaveChangesAsync();

        return person.Id;
    }

    public async Task AddReferencePhotosAsync(Guid personId, IReadOnlyList<ReferencePhotoInput> referencePhotos, string libraryPath)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        await SaveReferencePhotosAsync(db, personId, referencePhotos, libraryPath);
        await db.SaveChangesAsync();
    }

    public async Task DeletePersonAsync(Guid personId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var photos = await db.PersonReferencePhotos.Where(x => x.PersonId == personId).ToListAsync();
        foreach (var photo in photos)
        {
            try { if (File.Exists(photo.PhotoPath)) File.Delete(photo.PhotoPath); }
            catch (IOException) { /* best effort */ }
        }
        db.PersonReferencePhotos.RemoveRange(photos);

        var person = await db.People.FindAsync(personId);
        if (person is not null) db.People.Remove(person);

        var faceLinks = await db.MediaFaces.Where(x => x.MatchedPersonId == personId).ToListAsync();
        foreach (var f in faceLinks) { f.MatchedPersonId = null; f.UserConfirmed = false; f.Confidence = 0; }

        await db.SaveChangesAsync();
    }

    // Multiple mount aliases have pointed at the same physical Vibeke library
    // over the course of local Package3 testing (see docker-compose.yaml):
    // the NAS-equivalent path, and the literal host folder name before the
    // "/library/vibeke" alias mount was used consistently. All collapse to
    // the one canonical form so the same photo is never treated as "two
    // different files" just because of which path indexed it.
    private static string NormalizeMediaFilePath(string path) =>
        path
            .Replace("/volume1/docker/filesorter/library/", "/library/", StringComparison.OrdinalIgnoreCase)
            .Replace("/library/vibz-icloud-output/", "/library/vibeke/", StringComparison.OrdinalIgnoreCase);

    public async Task<List<PersonMatchResult>> FindMatchesAsync(Guid personId, double threshold = 0.45)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var referenceEmbeddings = await db.PersonReferencePhotos
            .Where(x => x.PersonId == personId)
            .Select(x => x.EmbeddingJson)
            .ToListAsync();

        if (referenceEmbeddings.Count == 0) return [];

        var references = referenceEmbeddings
            .Select(json => JsonSerializer.Deserialize<float[]>(json) ?? [])
            .Where(v => v.Length > 0)
            .ToList();

        var allFaces = await db.MediaFaces
            .Where(x => x.EmbeddingJson != "[]")
            .ToListAsync();

        var results = new List<PersonMatchResult>();

        // Group by file since a photo can have multiple detected faces - keep the
        // best-matching face per file so one photo doesn't appear twice in results.
        // Normalized because the same physical file was, for a while, indexed
        // under two different mount paths (a NAS-equivalent alias used for local
        // Package3 testing) - without this, the same photo shows up as two
        // "different" files and gets symlinked twice (once as "_1").
        foreach (var group in allFaces.GroupBy(x => NormalizeMediaFilePath(x.MediaFilePath)))
        {
            double best = 0;
            bool confirmed = false;

            foreach (var face in group)
            {
                float[] vector;
                try
                {
                    vector = JsonSerializer.Deserialize<float[]>(face.EmbeddingJson) ?? [];
                }
                catch (JsonException ex)
                {
                    // A single malformed embedding (e.g. from a prior storage
                    // fault) should never take down matching for everyone else.
                    _logger.LogWarning(ex, "Skipping unparsable embedding for {MediaFilePath}", face.MediaFilePath);
                    continue;
                }
                if (vector.Length == 0) continue;

                foreach (var reference in references)
                {
                    var similarity = CosineSimilarity(reference, vector);
                    if (similarity > best)
                    {
                        best = similarity;
                        confirmed = face.MatchedPersonId == personId && face.UserConfirmed;
                    }
                }
            }

            if (best >= threshold)
            {
                results.Add(new PersonMatchResult
                {
                    MediaFilePath = group.Key,
                    Confidence = best,
                    UserConfirmed = confirmed
                });
            }
        }

        return results.OrderByDescending(x => x.Confidence).ToList();
    }

    public async Task ConfirmMatchesAsync(Guid personId, IReadOnlyList<string> confirmedFilePaths, string libraryPath)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var person = await db.People.FindAsync(personId);
        if (person is null) return;

        foreach (var path in confirmedFilePaths)
        {
            var faces = await db.MediaFaces.Where(x => x.MediaFilePath == path).ToListAsync();
            foreach (var face in faces)
            {
                face.MatchedPersonId = personId;
                face.UserConfirmed = true;
            }
        }

        await db.SaveChangesAsync();

        var collections = await _collectionStore.LoadAsync(libraryPath);
        var existing = collections.FirstOrDefault(c => c.Name == person.Name);

        if (existing is null)
        {
            collections.Add(new MediaCollection { Name = person.Name, FilePaths = confirmedFilePaths.ToList() });
        }
        else
        {
            existing.FilePaths = existing.FilePaths
                .Union(confirmedFilePaths)
                .ToList();
        }

        await _collectionStore.SaveAsync(libraryPath, collections);
    }

    public async Task<List<UnnamedPersonCluster>> DiscoverUnnamedPeopleAsync(string libraryPath, double threshold = 0.5)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var unmatchedFaces = await db.MediaFaces
            .Where(x => x.MatchedPersonId == null && x.EmbeddingJson != "[]")
            .ToListAsync();

        // One face per file (same reasoning as FindMatchesAsync) - files with
        // several detected faces are rare, and this scopes discovery to one
        // attempt per photo, not per individual face.
        var perFile = new List<(string MediaFilePath, float[] Embedding)>();
        foreach (var group in unmatchedFaces.GroupBy(x => NormalizeMediaFilePath(x.MediaFilePath)))
        {
            foreach (var face in group)
            {
                float[] vector;
                try { vector = JsonSerializer.Deserialize<float[]>(face.EmbeddingJson) ?? []; }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Skipping unparsable embedding for {MediaFilePath}", face.MediaFilePath);
                    continue;
                }
                if (vector.Length == 0) continue;

                perFile.Add((group.Key, vector));
                break;
            }
        }

        // Greedy clustering: compare each face to every existing cluster's running-
        // average centroid, join the best match above threshold, else start a new
        // cluster. Simple and fine at the file counts a single library has -
        // not optimized for huge scale.
        var clusters = new List<(List<float[]> Embeddings, List<string> Files)>();

        foreach (var (path, embedding) in perFile)
        {
            (List<float[]> Embeddings, List<string> Files)? best = null;
            var bestSim = 0.0;

            foreach (var cluster in clusters)
            {
                var sim = CosineSimilarity(Average(cluster.Embeddings), embedding);
                if (sim >= threshold && sim > bestSim)
                {
                    bestSim = sim;
                    best = cluster;
                }
            }

            if (best is { } match)
            {
                match.Embeddings.Add(embedding);
                match.Files.Add(path);
            }
            else
            {
                clusters.Add(([embedding], [path]));
            }
        }

        // Groups under 3 photos are dropped as likely noise (a single stray
        // detection), same threshold Curator uses for burst-shot grouping.
        return clusters
            .Where(c => c.Files.Count >= 3)
            .OrderByDescending(c => c.Files.Count)
            .Select(c => new UnnamedPersonCluster
            {
                SampleMediaFilePath = c.Files[0],
                MediaFilePaths = c.Files
            })
            .ToList();
    }

    public async Task<Guid> NamePersonFromClusterAsync(string name, IReadOnlyList<string> clusterMediaFilePaths, string libraryPath)
    {
        if (clusterMediaFilePaths.Count == 0)
            throw new ArgumentException("Cluster has no photos", nameof(clusterMediaFilePaths));

        var referencePath = clusterMediaFilePaths[0];
        var bytes = await File.ReadAllBytesAsync(referencePath);
        var personId = await AddPersonAsync(
            name,
            [new ReferencePhotoInput(Path.GetFileName(referencePath), bytes)],
            libraryPath);

        // The clustering already established these faces belong together - no
        // need to re-run similarity comparison, just link them directly.
        await using var db = await _dbFactory.CreateDbContextAsync();
        foreach (var path in clusterMediaFilePaths)
        {
            var faces = await db.MediaFaces
                .Where(x => x.MatchedPersonId == null && x.MediaFilePath == path)
                .ToListAsync();

            foreach (var face in faces)
            {
                face.MatchedPersonId = personId;
                face.Confidence = 1.0;
                face.UserConfirmed = false;
            }
        }
        await db.SaveChangesAsync();

        return personId;
    }

    private static float[] Average(List<float[]> vectors)
    {
        var len = vectors[0].Length;
        var avg = new float[len];
        foreach (var v in vectors)
            for (var i = 0; i < len; i++)
                avg[i] += v[i] / vectors.Count;
        return avg;
    }

    private async Task SaveReferencePhotosAsync(
        MediaDbContext db,
        Guid personId,
        IReadOnlyList<ReferencePhotoInput> referencePhotos,
        string libraryPath)
    {
        var peopleDir = Path.Combine(libraryPath, ".package3", "people", personId.ToString("N"));
        Directory.CreateDirectory(peopleDir);

        var recognizer = _faceRecognitionFactory();

        foreach (var photo in referencePhotos)
        {
            var extension = Path.GetExtension(photo.FileName);
            if (string.IsNullOrWhiteSpace(extension)) extension = ".jpg";

            var savedPath = Path.Combine(peopleDir, $"{Guid.NewGuid():N}{extension}");
            await File.WriteAllBytesAsync(savedPath, photo.Bytes);

            var embeddings = await recognizer.ExtractFaceEmbeddingsAsync(savedPath);
            if (embeddings.Count == 0)
            {
                _logger.LogWarning("No face found in reference photo {FileName} for person {PersonId}", photo.FileName, personId);
                continue;
            }

            // A reference photo with several faces isn't disambiguated - use the
            // largest/first detection, which FaceONNX returns first in practice.
            db.PersonReferencePhotos.Add(new PersonReferencePhotoEntity
            {
                Id = Guid.NewGuid(),
                PersonId = personId,
                PhotoPath = savedPath,
                EmbeddingJson = JsonSerializer.Serialize(embeddings[0]),
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
        }

        (recognizer as IDisposable)?.Dispose();
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0;

        double dot = 0, magA = 0, magB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        if (magA == 0 || magB == 0) return 0;
        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }

    private static IEnumerable<string> EnumerateLibraryImages(string libraryPath)
    {
        if (!Directory.Exists(libraryPath)) yield break;

        foreach (var file in EnumerateDirectory(libraryPath))
            yield return file;
    }

    private static IEnumerable<string> EnumerateDirectory(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            if (MediaTypeHelper.IsImage(file) || MediaTypeHelper.IsVideo(file))
                yield return file;
        }

        foreach (var subDir in Directory.EnumerateDirectories(directory))
        {
            var name = Path.GetFileName(subDir);
            if (SkippedFolders.Contains(name) || name.StartsWith('@') || name.StartsWith('#') || name.StartsWith('.'))
                continue;

            foreach (var file in EnumerateDirectory(subDir))
                yield return file;
        }
    }
}
