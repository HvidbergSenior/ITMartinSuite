using ITMartin.Magic.Application.Models;
using ITMartin.OCR.Models;

namespace ITMartin.Magic.Application.Interfaces;

public interface ICardRecognitionService
{
    Task<RecognitionResult?> RecognizeAsync(
        OcrResult result);
}