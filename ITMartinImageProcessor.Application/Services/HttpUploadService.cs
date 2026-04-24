using System.Net.Http.Headers;

namespace ITMartinImageProcessor.Application.Services;

public class HttpUploadService
{
    private readonly HttpClient _client = new();

    public async Task UploadAsync(string filePath)
    {
        try
        {
            Console.WriteLine($"[HTTP] Preparing upload: {filePath}");

            using var content = new MultipartFormDataContent();

            var fileStream = File.OpenRead(filePath);
            var fileContent = new StreamContent(fileStream);

            fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

            content.Add(fileContent, "file", Path.GetFileName(filePath));
            content.Add(new StringContent("imageprocessor"), "key");

            var response = await _client.PostAsync(
                "https://bogshoppen.dk/webshopmedia/upload.php",
                content
            );

            Console.WriteLine($"[HTTP] Status: {response.StatusCode}");

            var result = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"[HTTP] Response: {result}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("[HTTP ERROR]");
            Console.WriteLine(ex);
        }
    }
}