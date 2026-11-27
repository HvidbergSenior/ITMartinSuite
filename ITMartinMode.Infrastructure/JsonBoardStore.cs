using System.Text.Json;
using ITMartinMode.Application;
using ITMartinMode.Domain;

namespace ITMartinMode.Infrastructure;

public sealed class JsonBoardStore(string dir) : IBoardStore
{
    private sealed record Dto(string? Idea, List<Note>? Notes);
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private string FileOf(string slug) => Path.Combine(dir, $"board-{slug}.json");

    public Board Load(string slug)
    {
        try
        {
            if (!File.Exists(FileOf(slug))) return new Board();
            var d = JsonSerializer.Deserialize<Dto>(File.ReadAllText(FileOf(slug)), Json);
            return d is null ? new Board() : new Board(d.Idea, d.Notes ?? new());
        }
        catch (JsonException) { return new Board(); }
    }

    public void Save(string slug, Board board)
    {
        Directory.CreateDirectory(dir);
        var tmp = FileOf(slug) + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(new Dto(board.Idea, board.Notes.ToList()), Json));
        File.Move(tmp, FileOf(slug), true);
    }
}
