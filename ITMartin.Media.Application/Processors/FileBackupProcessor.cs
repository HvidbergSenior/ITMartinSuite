using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class FileBackupProcessor
{
    public string Backup(
        MediaFile file,
        string backupFolder)
    {
        Directory.CreateDirectory(
            backupFolder);

        var target =
            Path.Combine(
                backupFolder,
                file.FileName);

        File.Copy(
            file.FullPath,
            target,
            true);

        return target;
    }
}