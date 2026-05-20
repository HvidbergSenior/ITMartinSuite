using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Processors;

public class FileDuplicateKeyProcessor
{
    public string Build(
        MediaFile file)
    {
        return
            $"{file.SizeBytes}_{file.FileName}";
    }
}