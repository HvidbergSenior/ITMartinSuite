namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class AlbumPruneResult
{
    public int AlbumsChecked { get; init; }
    public int AlbumsRemoved { get; init; }
    public int FilesRemoved { get; init; }
}
