namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

/// <summary>Faces found at one rotation: how many, and the detector's best confidence among them.</summary>
public sealed record RotationFaces(int Count, float MaxScore);

/// <summary>One detected face and how much it is rolled in the image (eye-line angle from the 68-point landmarks).</summary>
public sealed record FaceRoll(float Score, float RollDegrees, int SizePx);

public interface IFaceRecognitionService
{
    /// <summary>
    /// Detects every face in the image and returns a 512-dimension embedding for each.
    /// Empty if no face was found or the file could not be read as an image.
    /// </summary>
    Task<IReadOnlyList<float[]>> ExtractFaceEmbeddingsAsync(string filePath);

    /// <summary>
    /// Detection only - how many faces are in the image at each of the four
    /// rotations (0, 90, 180, 270 degrees clockwise). No landmarks, no
    /// embeddings, one decode and one downscale for all four; several times
    /// cheaper than four ExtractFaceEmbeddingsAsync calls on rotated copies.
    /// Used by the free orientation check. All zeros if the file is unreadable.
    /// </summary>
    Task<IReadOnlyDictionary<int, RotationFaces>> CountFacesPerRotationAsync(string filePath);

    /// <summary>
    /// Detects faces once (no rotation trials) and returns each face's roll
    /// angle from its landmarks. A face lying at ~90 degrees means the photo
    /// is sideways. preRotateDegrees rotates the image clockwise first - only
    /// for calibrating/testing the sign convention.
    /// </summary>
    Task<IReadOnlyList<FaceRoll>> DetectFaceRollsAsync(string filePath, int preRotateDegrees = 0);
}
