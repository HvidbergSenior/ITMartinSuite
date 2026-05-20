using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Processors;

public class FileDeleteProcessor
{
    public void Delete(
        MediaFile file)
    {
        if (!File.Exists(
                file.FullPath))
        {
            return;
        }

        File.Delete(
            file.FullPath);
    }
}