using FaceONNX;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ITMartin.Ai.Services;

public sealed class FaceOnnxRecognitionService : IFaceRecognitionService, IDisposable
{
    private readonly FaceDetector _faceDetector;
    private readonly Face68LandmarksExtractor _landmarksExtractor;
    private readonly FaceEmbedder _faceEmbedder;
    private readonly ILogger<FaceOnnxRecognitionService> _logger;
    private readonly object _lock = new();

    // Long side, in pixels, that detection runs at. Below this a group photo's
    // smallest faces start to drop out; above it the cost climbs for nothing.
    private const int DetectionMaxSide = 1600;

    // Faces smaller than this fraction of the image's longer side are skipped.
    // They are almost always photos-of-photos: framed pictures on a wall, a
    // screen in the background, a photo album page. Those faces are real to
    // the detector but carry almost no detail, so they match anyone loosely
    // and end up in the wrong person folder - and, worse, become references
    // when promoted (2026-09-14: a Malene reference turned out to be a wall
    // frame in a photo of Mathias). 2 % of a 4000 px photo is 80 px - a
    // distant person in a group shot is still comfortably above that.
    private const double MinFaceFractionOfLongSide = 0.02;

    public FaceOnnxRecognitionService(ILogger<FaceOnnxRecognitionService> logger)
    {
        _logger = logger;

        // Each ONNX session defaults to intra-op parallelism across all CPU cores.
        // With multiple instances running side by side (bulk indexing pool), that
        // causes massive thread oversubscription - pin each session to one thread
        // so real concurrency matches the outer pool size instead of exploding past it.
        var options = new SessionOptions { IntraOpNumThreads = 1, InterOpNumThreads = 1 };
        // Preserving FaceDetector's own defaults (0.3/0.4/0.5) since the SessionOptions
        // overload has no parameterless form.
        _faceDetector = new FaceDetector(options, 0.3f, 0.4f, 0.5f);
        _landmarksExtractor = new Face68LandmarksExtractor(options);
        _faceEmbedder = new FaceEmbedder(options);
    }

    public Task<IReadOnlyList<float[]>> ExtractFaceEmbeddingsAsync(string filePath)
    {
        // FaceONNX's ONNX InferenceSessions are not documented as thread-safe for
        // concurrent Forward() calls from the same instance, and this only ever
        // runs as part of a single-threaded library scan - a lock is cheap insurance.
        return Task.Run<IReadOnlyList<float[]>>(() =>
        {
            // A file the index knows about but that is no longer on disk is
            // ORDINARY, not an error: dedup removes the "_2" copies after they
            // were recorded, so a library re-index always meets some. Logged as
            // a stack trace it drowned the run - 1,655 multi-line traces on a
            // 5% test run on 2026-09-10, which buried every real progress line.
            // Checked before the lock so it costs nothing and never serialises.
            if (!File.Exists(filePath))
            {
                _logger.LogDebug("Skipping {FilePath} - no longer on disk", filePath);
                return [];
            }

            lock (_lock)
            {
                try
                {
                    using var original = Image.Load<Rgb24>(filePath);

                    // Two stages, at two resolutions, because detection and
                    // recognition want opposite things from the image.
                    //
                    // DETECTION runs on a downscaled copy. A modern phone photo
                    // is 12 megapixels; as three float planes that is ~146 MB,
                    // and the detector walks every pixel of it. At 1600 px on
                    // the long side there are six times fewer pixels and faces
                    // are still found reliably. This was the whole cost of face
                    // indexing on 2026-09-11 - 2h15m for 87,000 photos - and
                    // the memory pressure that held FaceIndexService to four
                    // workers.
                    //
                    // The EMBEDDING, though, is what recognition compares, and a
                    // face that is 100 px wide in the original is 40 px in the
                    // downscale - too little for a good one. So each detected
                    // box is mapped back onto the original and the embedding is
                    // taken from a full-resolution crop around it. Only the
                    // region around each face is converted to floats, so the
                    // memory saving survives too.
                    var longSide = Math.Max(original.Width, original.Height);
                    var scale = longSide > DetectionMaxSide ? DetectionMaxSide / (double)longSide : 1.0;

                    float[][,] detectionArray;
                    if (scale < 1.0)
                    {
                        using var small = original.Clone(x => x.Resize(
                            (int)Math.Round(original.Width * scale),
                            (int)Math.Round(original.Height * scale)));
                        detectionArray = ToFloatArray(small);
                    }
                    else
                    {
                        detectionArray = ToFloatArray(original);
                    }

                    // Largest face first. Callers that need "the" face of a
                    // photo - registering a reference photo above all - take
                    // the first embedding, and the detector's own order is
                    // not by size: on 2026-09-11 a selfie whose subject filled
                    // a third of the frame came back with a bystander first,
                    // and that bystander became the person's reference.
                    var faces = _faceDetector.Forward(detectionArray)
                        .OrderByDescending(f => (long)f.Box.Width * f.Box.Height)
                        .ToList();
                    var embeddings = new List<float[]>();

                    foreach (var face in faces)
                    {
                        if (face.Box.IsEmpty) continue;

                        // Box back in original coordinates.
                        var ox = (int)(face.Box.X / scale);
                        var oy = (int)(face.Box.Y / scale);
                        var ow = (int)(face.Box.Width / scale);
                        var oh = (int)(face.Box.Height / scale);
                        if (ow <= 0 || oh <= 0) continue;
                        if (Math.Max(ow, oh) < longSide * MinFaceFractionOfLongSide) continue;

                        // The embedding is taken from a crop with a generous
                        // margin around the box - alignment rotates the face
                        // by its tilt and needs hair and chin to work with.
                        // The margin is PADDED, never clamped: a face near the
                        // image edge, or a photo that IS a tight head crop,
                        // otherwise loses part of itself in the rotation and
                        // comes out as a different person. Measured on
                        // 2026-09-11: the same tilted face scored 0.94 against
                        // itself with margin and 0.29 as a tight crop - which
                        // is why every hand-cropped reference photo had been
                        // matching almost nothing.
                        var margin = (int)(Math.Max(ow, oh) * 0.6);
                        var cw = ow + 2 * margin;
                        var ch = oh + 2 * margin;

                        using var crop = new Image<Rgb24>(cw, ch, new Rgb24(114, 114, 114));
                        var srcX = Math.Max(0, ox - margin);
                        var srcY = Math.Max(0, oy - margin);
                        var srcW = Math.Min(original.Width, ox - margin + cw) - srcX;
                        var srcH = Math.Min(original.Height, oy - margin + ch) - srcY;
                        if (srcW <= 0 || srcH <= 0) continue;

                        using (var region = original.Clone(x => x.Crop(new Rectangle(srcX, srcY, srcW, srcH))))
                        {
                            var dest = new Point(srcX - (ox - margin), srcY - (oy - margin));
                            crop.Mutate(x => x.DrawImage(region, dest, 1f));
                        }

                        var cropArray = ToFloatArray(crop);
                        var boxInCrop = new System.Drawing.Rectangle(margin, margin, ow, oh);
                        var pts = _landmarksExtractor.Forward(cropArray, boxInCrop);
                        var alignedFace = FaceProcessingExtensions.Align(cropArray, boxInCrop, pts.RotationAngle);
                        embeddings.Add(_faceEmbedder.Forward(alignedFace));
                    }

                    return embeddings;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Face detection failed for {FilePath}", filePath);
                    return [];
                }
            }
        });
    }


    public Task<IReadOnlyDictionary<int, RotationFaces>> CountFacesPerRotationAsync(string filePath)
    {
        return Task.Run<IReadOnlyDictionary<int, RotationFaces>>(() =>
        {
            var none = new RotationFaces(0, 0f);
            var counts = new Dictionary<int, RotationFaces> { [0] = none, [90] = none, [180] = none, [270] = none };
            if (!File.Exists(filePath)) return counts;

            lock (_lock)
            {
                try
                {
                    using var original = Image.Load<Rgb24>(filePath);
                    var longSide = Math.Max(original.Width, original.Height);
                    var scale = longSide > DetectionMaxSide ? DetectionMaxSide / (double)longSide : 1.0;
                    using var small = scale < 1.0
                        ? original.Clone(x => x.Resize((int)Math.Round(original.Width * scale), (int)Math.Round(original.Height * scale)))
                        : original.Clone();
                    var smallLong = Math.Max(small.Width, small.Height);

                    foreach (var degrees in new[] { 0, 90, 180, 270 })
                    {
                        using var rotated = degrees == 0 ? small.Clone() : small.Clone(x => x.Rotate(degrees switch
                        {
                            90 => RotateMode.Rotate90,
                            180 => RotateMode.Rotate180,
                            _ => RotateMode.Rotate270,
                        }));
                        var faces = _faceDetector.Forward(ToFloatArray(rotated));
                        // Same tiny-face rule as ExtractFaceEmbeddingsAsync, in
                        // downscaled coordinates.
                        var real = faces.Where(f => !f.Box.IsEmpty && Math.Max(f.Box.Width, f.Box.Height) >= smallLong * MinFaceFractionOfLongSide).ToList();
                        counts[degrees] = new RotationFaces(real.Count, real.Count == 0 ? 0f : real.Max(f => f.Score));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Face counting failed for {FilePath}", filePath);
                }
            }
            return counts;
        });
    }


    public Task<IReadOnlyList<FaceRoll>> DetectFaceRollsAsync(string filePath, int preRotateDegrees = 0)
    {
        return Task.Run<IReadOnlyList<FaceRoll>>(() =>
        {
            var result = new List<FaceRoll>();
            if (!File.Exists(filePath)) return result;

            lock (_lock)
            {
                try
                {
                    using var loaded = Image.Load<Rgb24>(filePath);
                    using var original = preRotateDegrees switch
                    {
                        90 => loaded.Clone(x => x.Rotate(RotateMode.Rotate90)),
                        180 => loaded.Clone(x => x.Rotate(RotateMode.Rotate180)),
                        270 => loaded.Clone(x => x.Rotate(RotateMode.Rotate270)),
                        _ => loaded.Clone(),
                    };
                    var longSide = Math.Max(original.Width, original.Height);
                    var scale = longSide > DetectionMaxSide ? DetectionMaxSide / (double)longSide : 1.0;
                    float[][,] detectionArray;
                    if (scale < 1.0)
                    {
                        using var small = original.Clone(x => x.Resize((int)Math.Round(original.Width * scale), (int)Math.Round(original.Height * scale)));
                        detectionArray = ToFloatArray(small);
                    }
                    else detectionArray = ToFloatArray(original);

                    foreach (var face in _faceDetector.Forward(detectionArray).OrderByDescending(f => (long)f.Box.Width * f.Box.Height))
                    {
                        if (face.Box.IsEmpty) continue;
                        var ox = (int)(face.Box.X / scale);
                        var oy = (int)(face.Box.Y / scale);
                        var ow = (int)(face.Box.Width / scale);
                        var oh = (int)(face.Box.Height / scale);
                        if (ow <= 0 || oh <= 0) continue;
                        if (Math.Max(ow, oh) < longSide * MinFaceFractionOfLongSide) continue;

                        // Same padded crop as ExtractFaceEmbeddingsAsync - the
                        // landmark model needs the surroundings of the box.
                        var margin = (int)(Math.Max(ow, oh) * 0.6);
                        var cw = ow + 2 * margin;
                        var ch = oh + 2 * margin;
                        using var crop = new Image<Rgb24>(cw, ch, new Rgb24(114, 114, 114));
                        var srcX = Math.Max(0, ox - margin);
                        var srcY = Math.Max(0, oy - margin);
                        var srcW = Math.Min(original.Width, ox - margin + cw) - srcX;
                        var srcH = Math.Min(original.Height, oy - margin + ch) - srcY;
                        if (srcW <= 0 || srcH <= 0) continue;
                        using (var region = original.Clone(x => x.Crop(new Rectangle(srcX, srcY, srcW, srcH))))
                            crop.Mutate(x => x.DrawImage(region, new Point(srcX - (ox - margin), srcY - (oy - margin)), 1f));

                        var pts = _landmarksExtractor.Forward(ToFloatArray(crop), new System.Drawing.Rectangle(margin, margin, ow, oh));
                        result.Add(new FaceRoll(face.Score, pts.RotationAngle, Math.Max(ow, oh)));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Face roll detection failed for {FilePath}", filePath);
                }
            }
            return result;
        });
    }

    private static float[][,] ToFloatArray(Image<Rgb24> image)
    {
        var array = new[]
        {
            new float[image.Height, image.Width],
            new float[image.Height, image.Width],
            new float[image.Height, image.Width]
        };

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < accessor.Width; x++)
                {
                    array[2][y, x] = row[x].R / 255.0F;
                    array[1][y, x] = row[x].G / 255.0F;
                    array[0][y, x] = row[x].B / 255.0F;
                }
            }
        });

        return array;
    }

    public void Dispose()
    {
        _faceDetector.Dispose();
        _landmarksExtractor.Dispose();
        _faceEmbedder.Dispose();
    }
}
