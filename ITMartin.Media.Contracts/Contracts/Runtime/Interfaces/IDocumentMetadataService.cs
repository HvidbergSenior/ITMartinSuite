namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IDocumentMetadataService
{
    DateTime? GetCreationTime(string path);

    string? GetTitle(string path);

    string? GetAuthor(string path);

    int? GetPageCount(string path);
}
