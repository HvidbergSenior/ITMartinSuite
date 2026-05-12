namespace ITMartin.Magic.Application.Interfaces;

public interface ICardRecognitionService
{
    Task<string?> DetectCardNameAsync(
        string imagePath);
}