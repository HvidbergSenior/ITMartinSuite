namespace ITMartin.Magic.Application.Models;

public class CardFingerprint
{
    public bool WhiteBorder { get; set; }

    public bool OldFrame { get; set; }

    public double BorderBrightness { get; set; }

    public AverageColor AverageColor { get; set; } = new();
}

public class AverageColor
{
    public int R { get; set; }

    public int G { get; set; }

    public int B { get; set; }
}