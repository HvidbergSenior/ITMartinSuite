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
    private readonly IGpsService _gpsService;
    private readonly IAiEnrichmentService _aiEnrichmentService;
    private readonly ILogger<Package3Service> _logger;

    public Package3Service(
        IDbContextFactory<MediaDbContext> dbFactory,
        Func<IFaceRecognitionService> faceRecognitionFactory,
        IThumbnailService thumbnailService,
        ICollectionStore collectionStore,
        IGpsService gpsService,
        IAiEnrichmentService aiEnrichmentService,
        ILogger<Package3Service> logger)
    {
        _dbFactory = dbFactory;
        _faceRecognitionFactory = faceRecognitionFactory;
        _thumbnailService = thumbnailService;
        _collectionStore = collectionStore;
        _gpsService = gpsService;
        _aiEnrichmentService = aiEnrichmentService;
        _logger = logger;
    }

    // Shared by IndexFacesAsync's dated-reference cap and Pass 2's GPS dated-
    // reference cap (EstimateUndatedDatesAsync) - both need "a bounded but
    // representative sample of already-dated files", not the whole library.
    // Stratified by Year/Month, not a flat index-stride over the whole list -
    // a stride over an unbalanced list (e.g. one year with 5000 photos,
    // another with 50) would let the big year dominate the sample and could
    // skip a sparse year/month entirely. An even share per bucket guarantees
    // every dated month that exists contributes at least one reference file.
    private static List<string> SampleDatedFiles(List<string> datedFiles, int cap)
    {
        if (datedFiles.Count <= cap) return datedFiles;

        var byYearMonth = datedFiles
            .Select(f => (File: f, YearMonth: ExtractYearMonthFolder(f)))
            .GroupBy(x => x.YearMonth ?? "")
            .Select(g => g.Select(x => x.File).ToList())
            .ToList();

        var perBucket = Math.Max(1, cap / Math.Max(1, byYearMonth.Count));
        return byYearMonth
            .SelectMany(bucket => bucket.Count <= perBucket
                ? bucket
                : Enumerable.Range(0, perBucket).Select(i => bucket[(int)(i * (double)bucket.Count / perBucket)]))
            .Take(cap)
            .ToList();
    }

    public async Task IndexFacesAsync(string libraryPath, int? maxDatedReferenceFiles = null, CancellationToken cancellationToken = default)
    {
        var allFiles = EnumerateLibraryImages(libraryPath).ToList();

        List<string> files;
        if (maxDatedReferenceFiles is { } cap)
        {
            var undatedFiles = allFiles.Where(IsUnderUndatedFolder).ToList();
            var datedFiles = allFiles.Where(f => !IsUnderUndatedFolder(f)).ToList();
            files = undatedFiles.Concat(SampleDatedFiles(datedFiles, cap)).ToList();
        }
        else
        {
            files = allFiles;
        }

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
        // embedder) - the original cap of 2 was tuned for a memory-constrained
        // Docker container (ProcessorCount-1 OOM-killed it). FileSorter never
        // runs in a container though (confirmed permanent: always local,
        // bare-metal) - on real hardware with real RAM, leave 4 threads free
        // for the OS/everything else and use the rest.
        var degreeOfParallelism = Math.Min(12, Math.Max(1, Environment.ProcessorCount - 4));
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

                    await IndexFaceForFileAsync(db, libraryPath, file, recognizer, ct);

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

    private async Task IndexFaceForFileAsync(MediaDbContext db, string libraryPath, string file, IFaceRecognitionService recognizer, CancellationToken cancellationToken)
    {
        var relativePath = GetRelativePath(libraryPath, file);

        var alreadyFaceScanned = await db.MediaFaces.AnyAsync(x => x.RelativePath == relativePath, cancellationToken);
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
                    RelativePath = relativePath,
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
                    RelativePath = relativePath,
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
                    RelativePath = relativePath,
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

    // LibraryExportService stopped producing fixed "{MM:00}-{MonthName}"
    // folders (e.g. "08-August") a while back in favor of calendar-bucket
    // groups labeled "{groupIndex} {MonthName}" or, for a group spanning
    // more than one month, "{groupIndex} {MonthName}-{MonthName}" (e.g.
    // "8 August", "3 Marts-April" - see LibraryExportService's
    // SplitByCalendarBuckets). This regex still expected the old format, so
    // it matched nothing against any library using the new one - found
    // 2026-08-25 running EstimateUndatedDatesAsync against mie's real
    // library, where every dated folder uses the new scheme: the "already
    // dated" reference set this method needs was silently empty the whole
    // time, which fully explains the previously observed "0 moved, hard
    // ceiling" behavior (see feedback_undated_dating_ceiling) - it was never
    // actually a ceiling, the reference set just never populated.
    private static readonly System.Text.RegularExpressions.Regex YearMonthFolderPattern =
        new(@"[\\/](\d{4})[\\/](\d+ [A-Za-z]+(?:-[A-Za-z]+)?)[\\/]", System.Text.RegularExpressions.RegexOptions.Compiled);

    // Pulls the exact "{year}/{month-folder}" segment straight from an
    // already-dated file's own path (e.g. ".../Images/2025/02-February/x.jpg")
    // rather than reconstructing the month name - avoids any risk of the
    // reconstructed name not matching the culture/format the rest of the
    // pipeline already used for that folder.
    private static string? ExtractYearMonthFolder(string path)
    {
        var match = YearMonthFolderPattern.Match(path);
        return match.Success ? $"{match.Groups[1].Value}/{match.Groups[2].Value}" : null;
    }

    private static double HaversineMeters(double lat1, double lng1, double lat2, double lng2)
    {
        const double earthRadiusM = 6371000;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLng = (lng2 - lng1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return earthRadiusM * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    // LibraryExportService currently names this folder "Undated", but it was
    // "Udaterede" (Danish) before that rename - libraries sorted under the old
    // name (e.g. Mie's) still have it on disk under the old name, and nothing
    // ever renames an existing customer's folders in place. Checking only the
    // current name here silently found zero undated files for any pre-rename
    // library - both the folder-existence check and every per-file path match
    // need to recognize either name.
    // "Ikke i årsmapper" replaces the flat Undated/Udaterede convention for
    // libraries reorganized 2026-08-25 (mie's real library) into pattern-
    // based subfolders (Kamera (IMG), Facebook, GUID-eksport, etc.) under
    // Billeder instead of one flat bucket. The substring check below works
    // for it unchanged (any nesting depth), since it only looks for the
    // folder name itself appearing as a path segment - only FindUndatedFolder
    // needed a real fix, since Undated/Udaterede sit at the library root but
    // this one is nested under Billeder.
    private static readonly string[] UndatedFolderNames = ["Undated", "Udaterede", "Ikke i årsmapper"];

    private static bool IsUnderUndatedFolder(string path) =>
        UndatedFolderNames.Any(name =>
            path.Contains($"{Path.DirectorySeparatorChar}{name}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"/{name}/", StringComparison.OrdinalIgnoreCase));

    private static string? FindUndatedFolder(string libraryPath) =>
        UndatedFolderNames
            .SelectMany(name => new[] { Path.Combine(libraryPath, name), Path.Combine(libraryPath, "Billeder", name) })
            .FirstOrDefault(Directory.Exists);

    public async Task<UndatedEstimationResult> EstimateUndatedDatesAsync(
        string libraryPath,
        double faceThreshold = 0.5,
        double gpsToleranceMeters = 500,
        int? maxDatedReferenceFiles = null,
        CancellationToken cancellationToken = default)
    {
        // Populate/refresh MediaFaces for the whole library (skip-if-already-
        // indexed, free/local) so both the dated reference set and the
        // Undated candidates have embeddings to compare.
        await IndexFacesAsync(libraryPath, maxDatedReferenceFiles, cancellationToken);

        var movedByFace = 0;
        var movedByGps = 0;
        var stillUndated = new List<string>();

        var handled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ===== Pass 1: face match =====
        await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var allFaces = await db.MediaFaces
                .Where(x => x.EmbeddingJson != "[]")
                .ToListAsync(cancellationToken);

            var dated = new List<(float[] Embedding, string YearMonth)>();
            var undated = new List<(string Path, float[] Embedding)>();

            foreach (var face in allFaces)
            {
                float[] vector;
                try { vector = JsonSerializer.Deserialize<float[]>(face.EmbeddingJson) ?? []; }
                catch (JsonException) { continue; }
                if (vector.Length == 0) continue;

                var isUndated = IsUnderUndatedFolder(face.MediaFilePath);

                if (isUndated)
                {
                    undated.Add((face.MediaFilePath, vector));
                }
                else
                {
                    var ym = ExtractYearMonthFolder(face.MediaFilePath);
                    if (ym is not null) dated.Add((vector, ym));
                }
            }

            foreach (var (path, embedding) in undated)
            {
                if (!File.Exists(path)) continue;

                var best = 0.0;
                string? bestYearMonth = null;
                foreach (var (refEmbedding, ym) in dated)
                {
                    var sim = CosineSimilarity(embedding, refEmbedding);
                    if (sim > best) { best = sim; bestYearMonth = ym; }
                }

                if (best >= faceThreshold && bestYearMonth is not null && MoveIntoDatedFolder(path, bestYearMonth))
                {
                    handled.Add(path);
                    movedByFace++;
                }
            }
        }

        // ===== Pass 2: GPS proximity, for anything not already matched =====
        var undatedFolder = FindUndatedFolder(libraryPath);
        var undatedFiles = undatedFolder is not null
            // Raw Directory.EnumerateFiles, not EnumerateLibraryImages - the
            // undated folder never had a thumbnails/ subfolder problem before
            // (Undated/Udaterede were flat), but Ikke i årsmapper's pattern
            // subfolders each have their own, and this was walking straight
            // into them, doubling every count with the thumbnail copy.
            ? Directory.EnumerateFiles(undatedFolder, "*", SearchOption.AllDirectories)
                .Where(f => !Path.GetDirectoryName(f)!.EndsWith($"{Path.DirectorySeparatorChar}thumbnails", StringComparison.OrdinalIgnoreCase))
                .Where(f => !handled.Contains(f))
                .ToList()
            : [];

        if (undatedFiles.Count > 0)
        {
            var gpsService = _gpsService;
            var datedGps = new List<(double Lat, double Lng, string YearMonth)>();

            // Same cap as IndexFacesAsync's face-reference set - without it
            // this reads EXIF/GPS from the ENTIRE library serially (found
            // 2026-08-25 on mie's real library: ~43,000 files, single file at
            // a time, no cap at all - looked like a hang because it's I/O-
            // bound, not CPU-bound, but it was really just an unbounded scan
            // that happened to eventually finish).
            var gpsReferenceFiles = EnumerateLibraryImages(libraryPath)
                .Where(f => !IsUnderUndatedFolder(f))
                .ToList();
            if (maxDatedReferenceFiles is { } gpsCap)
                gpsReferenceFiles = SampleDatedFiles(gpsReferenceFiles, gpsCap);

            foreach (var file in gpsReferenceFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var coords = gpsService.GetCoordinates(file);
                if (coords is null) continue;

                var ym = ExtractYearMonthFolder(file);
                if (ym is not null) datedGps.Add((coords.Value.lat, coords.Value.lng, ym));
            }

            foreach (var file in undatedFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var coords = gpsService.GetCoordinates(file);
                if (coords is null) { stillUndated.Add(file); continue; }

                var bestDistance = double.MaxValue;
                string? bestYearMonth = null;
                foreach (var (lat, lng, ym) in datedGps)
                {
                    var d = HaversineMeters(coords.Value.lat, coords.Value.lng, lat, lng);
                    if (d < bestDistance) { bestDistance = d; bestYearMonth = ym; }
                }

                if (bestDistance <= gpsToleranceMeters && bestYearMonth is not null && MoveIntoDatedFolder(file, bestYearMonth))
                {
                    movedByGps++;
                }
                else
                {
                    stillUndated.Add(file);
                }
            }
        }

        return new UndatedEstimationResult
        {
            MovedByFaceMatch = movedByFace,
            MovedByGpsMatch = movedByGps,
            StillUndated = stillUndated.Count,
        };
    }

    // Text-only (filename/path) - no image bytes sent - so a large batch size
    // here is still cheap. MaxFiles is a hard ceiling per run (CLAUDE.md AI
    // cost discipline): an unexpectedly huge Unhandled folder gets truncated
    // rather than burning an unbounded number of API calls; already-processed
    // files (moved out of Unhandled) are naturally skipped on the next run.
    public async Task<UnhandledClassificationResult> ClassifyUnhandledFilesAsync(
        string libraryPath,
        int maxFiles = 500,
        CancellationToken cancellationToken = default)
    {
        const int batchSize = 100;

        var unhandledRoot = Path.Combine(libraryPath, "Unhandled");
        if (!Directory.Exists(unhandledRoot))
            return new UnhandledClassificationResult();

        var allFiles = Directory.EnumerateFiles(unhandledRoot, "*", SearchOption.AllDirectories).ToList();
        var skippedOverCap = Math.Max(0, allFiles.Count - maxFiles);
        var files = allFiles.Take(maxFiles).ToList();

        var reclassified = 0;
        var deleted = 0;
        var stillUnhandled = 0;

        foreach (var chunk in files.Chunk(batchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var idToPath = chunk.ToDictionary(_ => Guid.NewGuid(), p => p);
            var items = idToPath.Select(kv => (kv.Key, Path.GetRelativePath(unhandledRoot, kv.Value))).ToList();

            List<UnhandledClassificationItem> results;
            try
            {
                results = await _aiEnrichmentService.ClassifyUnhandledBatchAsync(items, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unhandled classification batch failed - leaving these files in place");
                stillUnhandled += chunk.Length;
                continue;
            }

            var resultById = results.ToDictionary(r => r.Id);

            foreach (var (id, path) in idToPath)
            {
                if (!resultById.TryGetValue(id, out var verdict) || verdict.Confidence < 0.6)
                {
                    stillUnhandled++;
                    continue;
                }

                var destRoot = verdict.Verdict switch
                {
                    "Images" or "Videos" or "Documents" or "Audio" =>
                        Path.Combine(libraryPath, verdict.Verdict, "FromUnhandled"),
                    "DeleteCandidate" =>
                        Path.Combine(libraryPath, "DeleteCandidates"),
                    _ => null
                };

                if (destRoot is null)
                {
                    stillUnhandled++;
                    continue;
                }

                if (TryMoveFile(path, destRoot))
                {
                    if (verdict.Verdict == "DeleteCandidate") deleted++;
                    else reclassified++;
                }
                else
                {
                    stillUnhandled++;
                }
            }

            await Task.Delay(1000, cancellationToken);
        }

        return new UnhandledClassificationResult
        {
            Reclassified = reclassified,
            MarkedForDeletion = deleted,
            StillUnhandled = stillUnhandled,
            SkippedOverCap = skippedOverCap
        };
    }

    private static bool TryMoveFile(string sourcePath, string destDir)
    {
        try
        {
            Directory.CreateDirectory(destDir);
            var dest = Path.Combine(destDir, Path.GetFileName(sourcePath));
            var i = 1;
            while (File.Exists(dest))
            {
                dest = Path.Combine(destDir, $"{Path.GetFileNameWithoutExtension(sourcePath)}_{i}{Path.GetExtension(sourcePath)}");
                i++;
            }
            File.Move(sourcePath, dest);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Undated files sit flat at Undated/{category}/{filename} (no year/month
    // subfolder - there was never a date to organize by). Moves into the
    // matched date's {category}/{year}/{month} folder, disambiguating on a
    // filename collision rather than silently overwriting.
    private static bool MoveIntoDatedFolder(string undatedPath, string yearMonth)
    {
        try
        {
            string category, libraryRoot;

            // New convention: .../{category}/Ikke i årsmapper/{pattern-subfolder}/file -
            // one level deeper than Undated/Udaterede, and the pattern-subfolder name
            // (e.g. "Kamera (IMG)") is NOT the category, so the old two-parents-up
            // logic below would both mis-derive the category and land two folders too
            // shallow. Found 2026-08-25 fixing this method for mie's real library,
            // where Billeder was reorganized into Ikke i årsmapper/<pattern> subfolders.
            var marker = $"{Path.DirectorySeparatorChar}Ikke i årsmapper{Path.DirectorySeparatorChar}";
            var markerIndex = undatedPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex >= 0)
            {
                var categoryDir = undatedPath[..markerIndex]; // .../{category}
                category = Path.GetFileName(categoryDir);
                libraryRoot = Path.GetDirectoryName(categoryDir)!;
            }
            else
            {
                category = Path.GetFileName(Path.GetDirectoryName(undatedPath)!); // Undated/{category}/file -> category
                var undatedRoot = Path.GetDirectoryName(Path.GetDirectoryName(undatedPath)!)!; // .../Undated
                libraryRoot = Path.GetDirectoryName(undatedRoot)!;
            }

            var destDir = Path.Combine(libraryRoot, category, yearMonth.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(destDir);

            var destPath = Path.Combine(destDir, Path.GetFileName(undatedPath));
            var attempt = 1;
            while (File.Exists(destPath))
            {
                destPath = Path.Combine(destDir,
                    $"{Path.GetFileNameWithoutExtension(undatedPath)}_{attempt}{Path.GetExtension(undatedPath)}");
                attempt++;
            }

            File.Move(undatedPath, destPath);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
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

    /// <summary>
    /// Normalizes to forward slashes so the same photo indexed via a Windows
    /// path (E:\mie\Billeder\...) and a Linux container mount (/library/mie/Billeder/...)
    /// produces the same RelativePath and is recognized as already-indexed either way.
    /// </summary>
    private static string GetRelativePath(string libraryPath, string file)
        => Path.GetRelativePath(libraryPath, file).Replace('\\', '/');

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
