using System.Security.Cryptography;
using ITMartinFileSorter.Domain.Interfaces;

namespace ITMartinFileSorter.Infrastructure.Services;

public class Sha256HashService : IHashService
{
    public string ComputeHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        var hashBytes = sha.ComputeHash(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
}