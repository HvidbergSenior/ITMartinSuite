using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ITMartin.Ai.Services;

// YOLOv10n (onnx-community/yolov10n, ~9 MB, bundled in Models/). Chosen over
// v8 because it is end-to-end: the model does its own non-max suppression
// and returns at most 300 boxes as [x1, y1, x2, y2, score, class] in input
// pixels, so there is no anchor decoding or NMS to get wrong here. CPU only,
// ~0.2 s per photo at 640 px including the JPEG decode - a whole family
// library in under an hour, at no cost. Same one-session-per-instance,
// one-thread-per-session, lock-around-Forward pattern as the face service.
public sealed class YoloObjectDetectionService : IObjectDetectionService, IDisposable
{
    private const int InputSize = 640;
    private const int MaxDetections = 300;

    private readonly InferenceSession _session;
    private readonly string _inputName;
    private readonly ILogger<YoloObjectDetectionService> _logger;
    private readonly object _lock = new();

    // COCO 80, in model class-id order.
    public static readonly string[] Labels =
    [
        "person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck", "boat", "traffic light",
        "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat", "dog", "horse", "sheep", "cow",
        "elephant", "bear", "zebra", "giraffe", "backpack", "umbrella", "handbag", "tie", "suitcase", "frisbee",
        "skis", "snowboard", "sports ball", "kite", "baseball bat", "baseball glove", "skateboard", "surfboard", "tennis racket", "bottle",
        "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple", "sandwich", "orange",
        "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", "chair", "couch", "potted plant", "bed",
        "dining table", "toilet", "tv", "laptop", "mouse", "remote", "keyboard", "cell phone", "microwave", "oven",
        "toaster", "sink", "refrigerator", "book", "clock", "vase", "scissors", "teddy bear", "hair drier", "toothbrush",
    ];

    public YoloObjectDetectionService(ILogger<YoloObjectDetectionService> logger)
    {
        _logger = logger;
        var modelPath = Path.Combine(AppContext.BaseDirectory, "Models", "yolov10n.onnx");
        if (!File.Exists(modelPath))
            throw new FileNotFoundException("Object detection model missing - Models/yolov10n.onnx must ship with the app", modelPath);

        var options = new SessionOptions { IntraOpNumThreads = 1, InterOpNumThreads = 1 };
        _session = new InferenceSession(modelPath, options);
        _inputName = _session.InputMetadata.Keys.First();
    }

    public Task<IReadOnlyList<DetectedObject>> DetectAsync(string filePath, double minConfidence = 0.35, CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<DetectedObject>>(() =>
        {
            if (!File.Exists(filePath)) return [];
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Letterbox into a 640x640 grey canvas so the aspect ratio
                // survives; the model was trained that way.
                using var image = Image.Load<Rgb24>(filePath);
                var scale = Math.Min((double)InputSize / image.Width, (double)InputSize / image.Height);
                var w = Math.Max(1, (int)Math.Round(image.Width * scale));
                var h = Math.Max(1, (int)Math.Round(image.Height * scale));
                image.Mutate(x => x.Resize(w, h));

                using var canvas = new Image<Rgb24>(InputSize, InputSize, new Rgb24(114, 114, 114));
                canvas.Mutate(x => x.DrawImage(image, new Point((InputSize - w) / 2, (InputSize - h) / 2), 1f));

                var tensor = new DenseTensor<float>([1, 3, InputSize, InputSize]);
                canvas.ProcessPixelRows(accessor =>
                {
                    for (var y = 0; y < InputSize; y++)
                    {
                        var row = accessor.GetRowSpan(y);
                        for (var x = 0; x < InputSize; x++)
                        {
                            tensor[0, 0, y, x] = row[x].R / 255f;
                            tensor[0, 1, y, x] = row[x].G / 255f;
                            tensor[0, 2, y, x] = row[x].B / 255f;
                        }
                    }
                });

                float[] output;
                lock (_lock)
                {
                    using var results = _session.Run([NamedOnnxValue.CreateFromTensor(_inputName, tensor)]);
                    output = results.First().AsEnumerable<float>().ToArray();
                }

                // One row per detection, six values each; the best score per
                // label is what a photo "has".
                var best = new Dictionary<string, double>();
                var rows = Math.Min(MaxDetections, output.Length / 6);
                for (var i = 0; i < rows; i++)
                {
                    var score = output[i * 6 + 4];
                    if (score < minConfidence) continue;
                    var cls = (int)output[i * 6 + 5];
                    if (cls < 0 || cls >= Labels.Length) continue;
                    var label = Labels[cls];
                    if (!best.TryGetValue(label, out var s) || score > s) best[label] = score;
                }

                return best.Select(kv => new DetectedObject(kv.Key, kv.Value)).OrderByDescending(d => d.Confidence).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Object detection failed for {FilePath}", filePath);
                return [];
            }
        }, cancellationToken);
    }

    public void Dispose() => _session.Dispose();
}
