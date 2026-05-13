namespace ITMartin.Magic.Application.Interfaces;

public interface IBorderDetectionService
{
    bool IsOldBorder(
        string imagePath);

    bool IsWhiteBorder(
        string imagePath);
}