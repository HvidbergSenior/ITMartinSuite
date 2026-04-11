namespace ITMartinFileSorter.Domain.Interfaces;

public interface IHashService
{
    string ComputeHash(string filePath);
}