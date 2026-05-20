namespace ITMartin.Media.Contracts.Entities;

public enum MediaFileStatus
{
    Initial,   // New file, user has not made a decision yet
    ToKeep,    // User marked to keep
    ToDelete   // User marked to delete
}