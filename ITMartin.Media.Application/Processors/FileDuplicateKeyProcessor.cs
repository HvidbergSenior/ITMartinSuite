using ITMartin.Media.Domain.Entities;

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