namespace ITMartinMediaSeller.Domain.Entities;

public class MediaItem
{
    public string Title { get; set; }
    public string Format { get; set; } // DVD / BluRay
    public int Quantity { get; set; }
}