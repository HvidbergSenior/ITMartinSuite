namespace ITMartinTestHub.Server.Data.Entities;

public sealed class TestStep
{
    public Guid    Id             { get; set; } = Guid.NewGuid();
    public Guid    AppEntryId     { get; set; }
    public int     Order          { get; set; }
    public string  Instruction    { get; set; } = "";
    public string? ExpectedResult { get; set; }
}
