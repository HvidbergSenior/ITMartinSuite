namespace ITMartinR6Assistant.Domain;

// One player's latest PreGameCheck.ps1 submission - kept so teammates can
// look at each other's setup for troubleshooting ("why can't X hear anyone"),
// not just the AI checklist shown once to whoever ran the script.
public class PlayerSetupRecord
{
    public string RawJson { get; set; } = "";
    public string Checklist { get; set; } = "";
    public DateTimeOffset SubmittedAtUtc { get; set; }
}
