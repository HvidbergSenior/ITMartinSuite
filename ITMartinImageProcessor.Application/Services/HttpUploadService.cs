using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;

namespace ITMartinImageProcessor.Application.Services;

public class HttpUploadService
{
    private readonly HttpClient _client = new();
    private readonly string _url;
    private readonly string _key;

    public HttpUploadService(IConfiguration config)
    {
        _url = config["Upload:Url"]!;
        _key = config["Upload:Key"]!;
    }

    public async Task UploadAsync(string filePath)
    {
        try
        {
            Console.WriteLine($"[UPLOAD] {filePath}");

            using var content = new MultipartFormDataContent();

            using var fileStream = File.OpenRead(filePath);
            using var fileContent = new StreamContent(fileStream);

            fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

            content.Add(fileContent, "file", Path.GetFileName(filePath));
            content.Add(new StringContent(_key), "key");

            var response = await _client.PostAsync(_url, content);

            Console.WriteLine($"[HTTP] Status: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HTTP ERROR] {ex}");
        }
    }
}