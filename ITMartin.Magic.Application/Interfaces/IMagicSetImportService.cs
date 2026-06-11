namespace ITMartin.Magic.Application.Interfaces;

public interface IMagicSetImportService
{
    Task ImportAsync(
        CancellationToken cancellationToken);
}