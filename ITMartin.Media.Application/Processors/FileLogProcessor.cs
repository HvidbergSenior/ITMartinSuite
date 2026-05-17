namespace ITMartin.Media.Application.Processors;

public class FileLogProcessor
{
    public void Write(
        string path,
        string message)
    {
        Directory.CreateDirectory(
            Path.GetDirectoryName(path)!);

        File.AppendAllText(
            path,
            $"{DateTime.UtcNow:u} {message}{Environment.NewLine}");
    }
}