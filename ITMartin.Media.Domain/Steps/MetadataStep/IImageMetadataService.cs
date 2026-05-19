namespace ITMartin.Media.Domain.Steps.MetadataStep;

public interface IImageMetadataService
{
    string GetModelFromFileName(string path);
    DateTime? GetCreationTime(string path);
    (int Width, int Height)? GetDimensions(
        string path);

    string? GetCameraModel(
        string path);

}
