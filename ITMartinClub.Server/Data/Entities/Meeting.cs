namespace ITMartinClub.Server.Data.Entities;

// Board meeting (Skelagerhøjen, 2026-10-02): there is always a referent (the chairman or someone else). Picking one creates the
// task "Skriv referat"; sending the minutes posts them as a Besked med kvittering and closes that task.
public sealed class Meeting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public DateTime Date { get; set; }                 // local Danish time as entered
    public string Place { get; set; } = "";
    public string ReferentName { get; set; } = "";
    public string Agenda { get; set; } = "";
    public string Referat { get; set; } = "";
    public Guid? ReferatTaskId { get; set; }            // the Assignment "Skriv referat …"
    public Guid? SentMessageId { get; set; }            // set when the minutes went out as a message
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
