using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class OpenAiBatchProcessor
{
    public List<List<MediaFile>>
        Split(
            IEnumerable<MediaFile> files,
            int batchSize = 10)
    {
        var result =
            new List<List<MediaFile>>();

        var current =
            new List<MediaFile>();

        foreach (var file in files)
        {
            current.Add(file);

            if (current.Count >= batchSize)
            {
                result.Add(current);

                current = [];
            }
        }

        if (current.Count > 0)
        {
            result.Add(current);
        }

        return result;
    }
}