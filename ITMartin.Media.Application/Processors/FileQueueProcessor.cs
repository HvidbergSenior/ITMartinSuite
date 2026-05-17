using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class FileQueueProcessor
{
    private readonly Queue<MediaFile>
        _queue = new();

    public void Enqueue(
        MediaFile file)
    {
        _queue.Enqueue(file);
    }

    public MediaFile? Dequeue()
    {
        if (_queue.Count == 0)
        {
            return null;
        }

        return _queue.Dequeue();
    }

    public int Count =>
        _queue.Count;
}