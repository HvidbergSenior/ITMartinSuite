namespace ITMartin.Media.Interfaces;

public interface IDocumentMetadataService
{
    DateTime? GetCreationTime(string path);
}
