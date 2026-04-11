namespace ITMartinFileSorter.Domain.Enums;

public enum MediaFileStatus
{
    Initial,   // New file, user has not made a decision yet
    ToKeep,    // User marked to keep
    ToDelete   // User marked to delete
}