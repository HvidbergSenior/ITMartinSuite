namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

// One thing the detector saw in a photo: a COCO class name ("dog", "cat",
// "bicycle", ...) and how sure it was. Boxes are deliberately not exposed -
// nothing downstream needs them, and leaving them out keeps the stored
// index to one row per (photo, label).
public sealed record DetectedObject(string Label, double Confidence);

// Local object detection - runs on the CPU with a bundled ONNX model, so
// scanning a whole library costs nothing but time. The face recognizer
// knows only human faces; this is how "every photo with the dog in it"
// becomes a folder. See YoloObjectDetectionService.
public interface IObjectDetectionService
{
    Task<IReadOnlyList<DetectedObject>> DetectAsync(string filePath, double minConfidence = 0.35, CancellationToken cancellationToken = default);
}

// Runs the detector over a library once, incrementally, and turns a label
// into a SmartFolders set - "Fie" for every photo with a dog.
public interface IObjectIndexService
{
    // Scans every image not already scanned. Returns how many were scanned
    // this run. Safe to re-run; only new files cost anything.
    Task<int> IndexAsync(string libraryPath, CancellationToken cancellationToken = default);

    // Files scanned so far, and how many carry the given label.
    Task<(int Scanned, int WithLabel)> StatusAsync(string libraryPath, string label);

    // (Re)builds SmartFolders/People/<folderName> from every scanned photo
    // that has the label at or above minConfidence, as hardlinks like the
    // person folders. Returns the number of files linked.
    Task<int> GenerateFolderAsync(string libraryPath, string label, string folderName, double minConfidence = 0.4, CancellationToken cancellationToken = default);
}
