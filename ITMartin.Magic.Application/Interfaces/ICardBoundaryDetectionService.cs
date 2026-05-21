namespace ITMartin.Magic.Application.Interfaces;

public interface ICardBoundaryDetectionService
{
    bool IsOldBorder(
        string imagePath);

    bool IsWhiteBorder(
        string imagePath);
}