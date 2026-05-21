using ITMartin.Magic.Application.Models;

namespace ITMartin.Magic.Application.Interfaces;

public interface IScryfallService
{
    Task<CardSearchResult?> SearchAsync(
        RecognitionResult cardRecognitionResult);
}