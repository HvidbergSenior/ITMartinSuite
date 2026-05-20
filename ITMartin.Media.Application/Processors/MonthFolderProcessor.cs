using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Processors;

public class MonthFolderProcessor
{
    public string Build(
        MediaFile file)
    {
        if (file.Month <= 0)
        {
            return "Unknown";
        }

        return file.Month
            .ToString("00");
    }
}