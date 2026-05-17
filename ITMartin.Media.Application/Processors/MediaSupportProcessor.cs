namespace ITMartin.Media.Application.Processors;

public class MediaSupportProcessor
{
    public bool IsSupportedMedia(
        string path)
    {
        var extension =
            Path.GetExtension(path);

        return
            FileScannerTypes
                .ImageExtensions
                .Contains(extension)
            ||
            FileScannerTypes
                .VideoExtensions
                .Contains(extension)
            ||
            FileScannerTypes
                .AudioExtensions
                .Contains(extension)
            ||
            DocumentExtensions
                .Contains(extension);
    }

    public bool SupportsOcr(
        string path)
    {
        var extension =
            Path.GetExtension(path);

        return extension.Equals(
                   ".jpg",
                   StringComparison
                       .OrdinalIgnoreCase)
               ||
               extension.Equals(
                   ".jpeg",
                   StringComparison
                       .OrdinalIgnoreCase)
               ||
               extension.Equals(
                   ".png",
                   StringComparison
                       .OrdinalIgnoreCase);
    }

    public static readonly HashSet<string>
        DocumentExtensions =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ".pdf",
                ".doc",
                ".docx",
                ".txt",
                ".xls",
                ".xlsx",
                ".ppt",
                ".pptx",
                ".csv"
            };
}