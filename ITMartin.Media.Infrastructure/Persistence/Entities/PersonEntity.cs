namespace ITMartin.Media.Infrastructure.Persistence.Entities;

public sealed class PersonEntity
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
