using System.Security.Cryptography;

namespace ITMartin.Media.Application.Processors;

public class FileChecksumProcessor
{
    public string Calculate(
        string path)
    {
        using var sha =
            SHA256.Create();

        using var stream =
            File.OpenRead(path);

        var hash =
            sha.ComputeHash(stream);

        return Convert.ToHexString(
            hash);
    }
}