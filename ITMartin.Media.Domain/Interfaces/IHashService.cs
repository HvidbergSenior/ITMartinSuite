namespace ITMartin.Media.Domain.Interfaces;

public interface IHashService
{
    string ComputeHash(string filePath);
}