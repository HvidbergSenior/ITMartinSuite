public class ProgressInfo
{
    public string Stage { get; set; } = "";
    public int WorkDone { get; set; }
    public int TotalWork { get; set; }

    public double Percent =>
        TotalWork == 0 ? 0 :
            (double)WorkDone / TotalWork * 100;

    public double SpeedPerSecond { get; set; }

    public TimeSpan? EstimatedRemaining { get; set; }

    public bool IsCompleted => WorkDone >= TotalWork;
}