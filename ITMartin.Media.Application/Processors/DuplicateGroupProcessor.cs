using ITMartin.Media.Domain.Models;

namespace ITMartin.Media.Application.Processors;

public class DuplicateGroupProcessor
{
    public int Count(
        IEnumerable<DuplicateGroup> groups)
    {
        return groups.Count();
    }
}