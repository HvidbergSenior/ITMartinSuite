public interface IMagicSetImportService
{
    Task ImportAsync(
        CancellationToken cancellationToken);
}