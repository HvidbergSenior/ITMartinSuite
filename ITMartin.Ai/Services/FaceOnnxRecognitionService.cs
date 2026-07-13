using FaceONNX;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ITMartin.Ai.Services;

public sealed class FaceOnnxRecognitionService : IFaceRecognitionService, IDisposable
{
    private readonly FaceDetector _faceDetector = new();
    private readonly Face68LandmarksExtractor _landmarksExtractor = new();
    private readonly FaceEmbedder _faceEmbedder = new();
    private readonly ILogger<FaceOnnxRecognitionService> _logger;
    private readonly object _lock = new();

    public FaceOnnxRecognitionService(ILogger<FaceOnnxRecognitionService> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<float[]>> ExtractFaceEmbeddingsAsync(string filePath)
    {
        // FaceONNX's ONNX InferenceSessions are not documented as thread-safe for
        // concurrent Forward() calls from the same instance, and this only ever
        // runs as part of a single-threaded library scan - a lock is cheap insurance.
        return Task.Run<IReadOnlyList<float[]>>(() =>
        {
            lock (_lock)
            {
                try
                {
                    using var image = Image.Load<Rgb24>(filePath);
                    var array = ToFloatArray(image);

                    var faces = _faceDetector.Forward(array);
                    var embeddings = new List<float[]>();

                    foreach (var face in faces)
                    {
                        if (face.Box.IsEmpty) continue;

                        var points = _landmarksExtractor.Forward(array, face.Box);
                        var aligned = FaceProcessingExtensions.Align(array, face.Box, points.RotationAngle);
                        embeddings.Add(_faceEmbedder.Forward(aligned));
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
