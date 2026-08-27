using ITMartin.Documents.Models;

namespace ITMartin.Documents.Interfaces;

public interface IDocxImportService
{
    List<ParsedDocumentSection> ParseDocx(Stream docxStream);
}
