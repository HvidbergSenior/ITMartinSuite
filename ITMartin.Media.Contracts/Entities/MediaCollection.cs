namespace ITMartin.Media.Contracts.Entities;

public class MediaCollection
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public List<string> FilePaths { get; set; } = [];
}
