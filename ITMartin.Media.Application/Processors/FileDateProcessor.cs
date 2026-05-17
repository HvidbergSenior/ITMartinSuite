using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class FileDateProcessor
{
    public int GetYear(
        MediaFile file)
    {
        return file.Year;
    }

    public int GetMonth(
        MediaFile file)
    {
        return file.Month;
    }

    public DateTime? GetDate(
        MediaFile file)
    {
        return file.CreatedAt;
    }
}