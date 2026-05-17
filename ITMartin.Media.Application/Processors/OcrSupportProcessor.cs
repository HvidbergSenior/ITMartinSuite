namespace ITMartin.Media.Application.Processors;

public class OcrSupportProcessor
{
    public bool Supports(
        string path)
    {
        var extension =
            Path.GetExtension(path);

        return extension.Equals(
                   ".jpg",
                   StringComparison
                       .OrdinalIgnoreCase)
               ||
               extension.Equals(
                   ".jpeg",
                   StringComparison
                       .OrdinalIgnoreCase)
               ||
               extension.Equals(
                   ".png",
                   StringComparison
                       .OrdinalIgnoreCase);
    }
}