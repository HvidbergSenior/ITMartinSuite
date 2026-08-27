namespace ITMartinR6Assistant.Domain;

// Manual fallback values for the Specifikationer card on the homepage - only
// filled in for fields that couldn't be determined automatically (from the
// browser, or from the player's last PreGameCheck.ps1 submission). Empty
// string means "not manually set", not "confirmed empty".
public class PlayerSpecs
{
    public string Cpu { get; set; } = "";
    public string GraphicsCard { get; set; } = "";
    public string Ram { get; set; } = "";
    public string Mouse { get; set; } = "";
    public string Keyboard { get; set; } = "";
    public string Screen { get; set; } = "";
    public string Headset { get; set; } = "";
    public string HeadsetSoftware { get; set; } = "";
    public string OperatingSystem { get; set; } = "";
}
