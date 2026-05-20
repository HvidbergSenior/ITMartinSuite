using System.IO.Compression;
using System.Xml.Linq;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

namespace ITMartin.Media.Infrastructure.Metadata;

public class DocumentMetadataService : IDocumentMetadataService
{
    public DateTime? GetCreationTime(string path)
    {
        try
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();

            if (ext != ".docx" &&
                ext != ".xlsx" &&
                ext != ".pptx")
            {
                return null;
            }

            using var archive = ZipFile.OpenRead(path);

            var coreEntry = archive.GetEntry("docProps/core.xml");

            if (coreEntry == null)
                return null;

            using var stream = coreEntry.Open();

            var xml = XDocument.Load(stream);

            XNamespace dcterms =
                "http://purl.org/dc/terms/";

            var createdElement =
                xml.Descendants(dcterms + "created")
                    .FirstOrDefault();

            if (createdElement != null &&
                DateTime.TryParse(
                    createdElement.Value,
                    out var created))
            {
                return created;
            }

            var modifiedElement =
                xml.Descendants(dcterms + "modified")
                    .FirstOrDefault();

            if (modifiedElement != null &&
                DateTime.TryParse(
                    modifiedElement.Value,
                    out var modified))
            {
                return modified;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public string? GetTitle(string path)
    {
        throw new NotImplementedException();
    }

    public string? GetAuthor(string path)
    {
        throw new NotImplementedException();
    }

    public int? GetPageCount(string path)
    {
        throw new NotImplementedException();
    }
}