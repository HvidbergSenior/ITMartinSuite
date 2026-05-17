namespace ITMartin.Media.Application.Processors;

public class FileRestoreProcessor
{
    public void Restore(
        string backupPath,
        string originalPath)
    {
        Directory.CreateDirectory(
            Path.GetDirectoryName(
                originalPath)!);

        File.Copy(
            backupPath,
            originalPath,
            true);
    }
}