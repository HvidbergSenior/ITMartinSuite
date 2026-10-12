namespace ITMartinHjem.Domain;

// A question a visitor asked in the chat and Martin's answer, shown on itmartin.dk so the next person with the
// same question finds it (user 2026-10-12: "they ask the same questions and also i want transparency").
// Never carries a name: Martin picks what goes up, the text is anonymised first and he can edit it before publishing.
public sealed class PublicQuestion
{
    public const int MaxQuestion = 1000, MaxAnswer = 3000, MaxTopic = 40;

    public int Id { get; private set; }
    public string Question { get; private set; } = "";
    public string Answer { get; private set; } = "";
    public string Topic { get; private set; } = "";
    // Which site shows it: QuestionRules.ITMartin or QuestionRules.Bogshoppen.
    public string Site { get; private set; } = QuestionRules.ITMartin;
    public DateTime PublishedAt { get; private set; }
    // The chat it came from - only so Svar can show "already on the page"; never shown publicly.
    public int? SourceThreadId { get; private set; }

    private PublicQuestion() { }   // EF

    public static PublicQuestion Publish(string question, string answer, string topic, DateTime now, int? sourceThreadId, string? site = null)
    {
        var q = new PublicQuestion { PublishedAt = now, SourceThreadId = sourceThreadId };
        q.Edit(question, answer, topic, site);
        return q;
    }

    public void Edit(string question, string answer, string topic, string? site = null)
    {
        var q = (question ?? "").Trim();
        var a = (answer ?? "").Trim();
        if (q.Length == 0) throw new ArgumentException("Spørgsmålet mangler.");
        if (a.Length == 0) throw new ArgumentException("Svaret mangler.");
        Question = q.Length > MaxQuestion ? q[..MaxQuestion] : q;
        Answer = a.Length > MaxAnswer ? a[..MaxAnswer] : a;
        Topic = QuestionRules.NormalizeTopic(topic);
        Site = QuestionRules.NormalizeSite(site);
    }
}
