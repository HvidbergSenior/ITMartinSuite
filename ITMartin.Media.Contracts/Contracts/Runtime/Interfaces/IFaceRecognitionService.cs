namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IFaceRecognitionService
{
    /// <summary>
    /// Detects every face in the image and returns a 512-dimension embedding for each.
    /// Empty if no face was found or the file could not be read as an image.
    /// </summary>
    Task<IReadOnlyList<float[]>> ExtractFaceEmbeddingsAsync(string filePath);
}
