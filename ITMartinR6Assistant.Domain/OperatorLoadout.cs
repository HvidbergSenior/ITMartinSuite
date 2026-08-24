namespace ITMartinR6Assistant.Domain;

public class OperatorLoadout
{
    public string Primary { get; set; } = "";
    public string Secondary { get; set; } = "";
    public string Gadget { get; set; } = "";

    public bool IsEmpty => string.IsNullOrWhiteSpace(Primary) && string.IsNullOrWhiteSpace(Secondary) && string.IsNullOrWhiteSpace(Gadget);
}
