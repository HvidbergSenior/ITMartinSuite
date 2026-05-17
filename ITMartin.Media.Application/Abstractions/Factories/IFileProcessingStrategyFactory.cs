using ITMartin.Media.Application.Abstractions.Strategies;

namespace ITMartin.Media.Application.Abstractions.Factories;

public interface IFileProcessingStrategyFactory
{
    IFileProcessingStrategy Resolve(string extension);
}