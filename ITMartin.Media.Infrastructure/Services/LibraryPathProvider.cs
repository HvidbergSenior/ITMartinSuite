
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ITMartin.Media.Infrastructure.Services;

public sealed class LibraryPathProvider
    : ILibraryPathProvider
{
    private readonly IConfiguration
        _configuration;

    public LibraryPathProvider(
        IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string LibraryRoot
    {
        get
        {
            var value =
                _configuration[
                    "MediaSettings:LibraryRoot"];

            if (string.IsNullOrWhiteSpace(
                    value))
            {
                throw new InvalidOperationException(
                    "LibraryRoot is not configured");
            }

            return value;
        }
    }
}