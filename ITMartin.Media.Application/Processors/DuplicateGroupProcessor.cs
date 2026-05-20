using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Processors;

public class DuplicateGroupProcessor
{
    public int Count(
        IEnumerable<DuplicateGroup> groups)
    {
        return groups.Count();
    }
}