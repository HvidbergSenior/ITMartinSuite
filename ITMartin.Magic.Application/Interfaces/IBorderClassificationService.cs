namespace ITMartin.Magic.Application.Interfaces;

public interface IBorderClassificationService
{
    bool IsOldBorder(
        string imagePath);

    bool IsWhiteBorder(
        string imagePath);
}