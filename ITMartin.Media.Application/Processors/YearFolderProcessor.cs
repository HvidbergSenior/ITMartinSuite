using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class YearFolderProcessor
{
    public string Build(
        MediaFile file)
    {
        return file.Year <= 0
            ? "Unknown"
            : file.Year.ToString();
    }
}