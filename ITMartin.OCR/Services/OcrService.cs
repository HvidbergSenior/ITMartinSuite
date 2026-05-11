using ITMartin.OCR.Interfaces;
using Tesseract;

namespace ITMartin.OCR.Services;

public class OcrService : IOcrService
{
    public async Task<string?> ExtractTextAsync(string path)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var engine = new TesseractEngine(
                    Path.Combine(
                        AppContext.BaseDirectory,
                        "tessdata"),
                    "eng",
                    EngineMode.Default);
                
                using var img = Pix.LoadFromFile(path);

                using var page = engine.Process(img);
                var text = page.GetText();

                Console.WriteLine($"OCR RESULT: [{text}]");

                return text;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);

                return null;
            }
        });
    }
}