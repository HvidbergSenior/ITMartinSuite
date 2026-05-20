using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Processors;

public class PreviewPathProcessor
{
    public string Build(
        MediaFile file,
        string root)
    {
        return Path.Combine(
            root,
            $"{file.Id}_preview.jpg");
    }
}