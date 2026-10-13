namespace ITMartinClub.Domain;

/// <summary>Something that belongs to one team (Group) - it is only ever read or changed through that team.</summary>
public interface ITeamOwned
{
    Guid Id { get; }
    Guid GroupId { get; }
}
