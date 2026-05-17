namespace ITMartin.Media.Application.Processors;

public class QueueBatchProcessor<T>
{
    public List<List<T>> Split(
        IEnumerable<T> items,
        int batchSize)
    {
        var result =
            new List<List<T>>();

        var batch =
            new List<T>();

        foreach (var item in items)
        {
            batch.Add(item);

            if (batch.Count >= batchSize)
            {
                result.Add(batch);

                batch = [];
            }
        }

        if (batch.Count > 0)
        {
            result.Add(batch);
        }

        return result;
    }
}